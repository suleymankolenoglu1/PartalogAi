using Katalogcu.API.Services;
using Katalogcu.API.Contracts.Chat;
using Katalogcu.Application.Features.Chat.Commands.AskChat;
using Katalogcu.Application.Features.Chat.Commands.SaveChatFeedback;
using Katalogcu.Application.Features.Chat.Commands.SaveVisualFeedback;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Application.Features.Chat.Common;
using Katalogcu.Application.Features.Chat.Queries.ResolveChatCatalogAccess;
using Katalogcu.Application.Features.Chat.Queries.ResolveChatUser;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IPartalogAiService _aiService;
        private readonly ISender _sender;
        private readonly ILogger<ChatController> _logger;
        private readonly IChatStreamProxyService _chatStreamProxyService;
        private readonly IAiUsageQuotaService _aiUsageQuotaService;
        private readonly IAiCapacityGuard _aiCapacityGuard;

        public ChatController(
            IPartalogAiService aiService,
            ISender sender,
            ILogger<ChatController> logger,
            IChatStreamProxyService chatStreamProxyService,
            IAiUsageQuotaService aiUsageQuotaService,
            IAiCapacityGuard aiCapacityGuard)
        {
            _aiService = aiService;
            _sender = sender;
            _logger = logger;
            _chatStreamProxyService = chatStreamProxyService;
            _aiUsageQuotaService = aiUsageQuotaService;
            _aiCapacityGuard = aiCapacityGuard;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private static List<Guid> ParseCatalogIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
            try
            {
                var strIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();
                return strIds
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .Distinct()
                    .ToList();
            }
            catch
            {
                try
                {
                    var guidIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? new();
                    return guidIds.Where(g => g != Guid.Empty).Distinct().ToList();
                }
                catch
                {
                    return new List<Guid>();
                }
            }
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromForm] AiChatRequestWithHistoryDto request)
        {
            var validationError = UploadValidation.ValidateImage(request.Image, required: false);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(validationError);
            }

            try
            {
                var tokenUserId = GetCurrentUserId();

                var catalogIdsJson = Request.HasFormContentType ? Request.Form["catalog_ids"].ToString() : null;
                var requestedCatalogIds = ParseCatalogIds(catalogIdsJson);
                var accessResult = await _sender.Send(new ResolveChatCatalogAccessQuery(
                    tokenUserId,
                    request.PublicToken,
                    requestedCatalogIds));
                if (!accessResult.IsSuccess)
                {
                    return BadRequest(accessResult.ErrorMessage ?? "Katalog bilgisi bulunamadı.");
                }

                var catalogIds = accessResult.Value!.CatalogIds.ToList();
                _logger.LogInformation("Chat request catalogs: {CatalogCount}", catalogIds.Count);

                var userResult = await _sender.Send(new ResolveChatUserQuery(tokenUserId, request.PublicToken));
                if (!userResult.IsSuccess)
                {
                    return BadRequest(userResult.ErrorMessage ?? "Geçerli kullanıcı veya public token gerekli.");
                }

                var usage = await _aiUsageQuotaService.GetCurrentUsageAsync(userResult.Value!.UserId, HttpContext.RequestAborted);
                if (!usage.AiEnabled || (!usage.Unlimited && usage.RemainingThisMonth <= 0))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "AI sorgu limitinize ulaştınız, planınızı yükseltin" });
                }

                await using var capacityLease = await _aiCapacityGuard.TryAcquireAsync(
                    userResult.Value.UserId,
                    request.PublicToken,
                    HttpContext.RequestAborted);
                if (capacityLease is null)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        message = _aiCapacityGuard.BusyMessage,
                        reason = "ai_capacity_limited"
                    });
                }

                // 1. History Parse
                var chatHistory = new List<ChatMessageDto>();
                if (!string.IsNullOrEmpty(request.History))
                {
                    try
                    {
                        chatHistory = JsonConvert.DeserializeObject<List<ChatMessageDto>>(request.History) ?? new();
                    }
                    catch { _logger.LogWarning("History parse edilemedi, sohbet sıfırdan başlıyor."); }
                }

                var catalogIdStrings = catalogIds.Select(c => c.ToString()).ToList();

                // 2. Servis İsteği Hazırlığı
                var aiRequest = new AiChatRequestDto
                {
                    Text = request.Text,
                    Image = request.Image?.ToUploadedFile(),
                    History = chatHistory,
                    CatalogIds = catalogIdStrings,
                    ContextJson = request.ContextJson,
                    UserPlan = usage.Plan.ToString(),
                    AiLimitPerMonth = usage.MonthlyLimit,
                    AiUsedThisMonth = usage.UsedThisMonth
                };

                // 3. AI Analizi (Python)
                var aiResponse = await _aiService.GetExpertChatResponseAsync(aiRequest);

                var debugIntentJson = aiResponse.DebugIntent switch
                {
                    JsonElement je => je.GetRawText(),
                    null => null,
                    _ => System.Text.Json.JsonSerializer.Serialize(aiResponse.DebugIntent)
                };
                var requestedMachineBrand = ReadDebugIntentString(debugIntentJson, "brand");
                var requestedMachineModel = ReadDebugIntentString(debugIntentJson, "machine_model");
                var requestedMachineVariant = ReadDebugIntentString(debugIntentJson, "machine_variant");

                var sourceInputs = (aiResponse.Sources ?? new List<ChatSourceDto>())
                    .Select(source => new ChatSourceInput
                    {
                        Code = source.Code,
                        Name = source.Name,
                        Model = source.Model,
                        LegacyModel = source.LegacyModel,
                        Description = source.Description,
                        LegacyDescription = source.LegacyDescription,
                        Query = source.Query,
                        Similarity = source.Similarity,
                        MatchReason = source.MatchReason,
                        ConfidenceLabel = source.ConfidenceLabel,
                        RequiresVerification = source.RequiresVerification,
                        Fallback = source.Fallback,
                        FallbackReason = source.FallbackReason,
                        CatalogId = source.CatalogId ?? source.LegacyCatalogId,
                        PageNumber = source.PageNumber ?? source.LegacyPageNumber,
                        RequestedMachineBrand = requestedMachineBrand,
                        RequestedMachineModel = requestedMachineModel,
                        RequestedMachineVariant = requestedMachineVariant
                    })
                    .ToList();

                var chatResult = await _sender.Send(new AskChatCommand(
                    request.Text,
                    aiResponse.Answer,
                    debugIntentJson,
                    catalogIds,
                    sourceInputs));

                if (!chatResult.IsSuccess)
                {
                    return chatResult.ErrorCode switch
                    {
                        "validation" => BadRequest(chatResult.ErrorMessage),
                        _ => StatusCode(500, chatResult.ErrorMessage ?? "Chat yanıtı üretilemedi.")
                    };
                }

                var response = chatResult.Value!;
                if (IsBillableAiResponse(aiResponse))
                {
                    var aiQuota = await _aiUsageQuotaService.ConsumeAsync(userResult.Value.UserId, HttpContext.RequestAborted);
                    if (!aiQuota.Allowed)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = aiQuota.Message });
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat Controller Hatası");
                return StatusCode(500, new { error = "Sistem hatası oluştu. Lütfen tekrar deneyin." });
            }
        }

        [HttpPost("ask-stream")]
        public async Task AskStream([FromForm] AiChatRequestWithHistoryDto request)
        {
            var validationError = UploadValidation.ValidateImage(request.Image, required: false);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                Response.StatusCode = 400;
                return;
            }

            var tokenUserId = GetCurrentUserId();

            var catalogIdsJson = Request.HasFormContentType ? Request.Form["catalog_ids"].ToString() : null;
            var requestedCatalogIds = ParseCatalogIds(catalogIdsJson);
            var accessResult = await _sender.Send(new ResolveChatCatalogAccessQuery(
                tokenUserId,
                request.PublicToken,
                requestedCatalogIds));
            if (!accessResult.IsSuccess)
            {
                Response.StatusCode = 400;
                return;
            }

            var catalogIds = accessResult.Value!.CatalogIds.ToList();
            var catalogIdStrings = catalogIds.Select(c => c.ToString()).ToList();

            var userResult = await _sender.Send(new ResolveChatUserQuery(tokenUserId, request.PublicToken));
            if (!userResult.IsSuccess)
            {
                Response.StatusCode = 400;
                await WriteStreamMessageAsync(userResult.ErrorMessage ?? "Geçerli kullanıcı veya public token gerekli.");
                return;
            }

            var usage = await _aiUsageQuotaService.GetCurrentUsageAsync(userResult.Value!.UserId, HttpContext.RequestAborted);
            if (!usage.AiEnabled || (!usage.Unlimited && usage.RemainingThisMonth <= 0))
            {
                Response.StatusCode = 200;
                await WriteStreamMessageAsync("AI sorgu limitinize ulaştınız, planınızı yükseltin", "plan_limit");
                return;
            }

            await using var capacityLease = await _aiCapacityGuard.TryAcquireAsync(
                userResult.Value.UserId,
                request.PublicToken,
                HttpContext.RequestAborted);
            if (capacityLease is null)
            {
                Response.StatusCode = StatusCodes.Status200OK;
                await WriteStreamMessageAsync(_aiCapacityGuard.BusyMessage, "ai_capacity_limited");
                return;
            }

            try
            {
                var proxyResult = await _chatStreamProxyService.ProxyAskStreamAsync(
                    Response,
                    request.Text,
                    request.History,
                    request.ContextJson,
                    catalogIdStrings,
                    request.Image,
                    usage.Plan.ToString(),
                    usage.MonthlyLimit,
                    usage.UsedThisMonth,
                    HttpContext.RequestAborted);

                if (proxyResult.Billable)
                {
                    var aiQuota = await _aiUsageQuotaService.ConsumeAsync(userResult.Value.UserId, HttpContext.RequestAborted);
                    if (!aiQuota.Allowed)
                    {
                        _logger.LogWarning(
                            "Stream AI quota consume failed after billable response. UserId={UserId} Message={Message}",
                            userResult.Value.UserId,
                            aiQuota.Message);
                    }
                }
            }
            catch
            {
                // Hata logu servis tarafında tutuluyor.
            }
        }

        private async Task WriteStreamMessageAsync(string message, string fallbackReason = "controller_message")
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            await Response.WriteAsync(
                ChatStreamEventContract.ToSseDataLine(
                    ChatStreamEventContract.CreateSources(
                        Array.Empty<object>(),
                        fallbackUsed: true,
                        fallbackReason: fallbackReason)));
            await Response.WriteAsync(
                ChatStreamEventContract.ToSseDataLine(
                    ChatStreamEventContract.CreateToken(
                        message,
                        fallbackUsed: true,
                        fallbackReason: fallbackReason)));
            await Response.WriteAsync(
                ChatStreamEventContract.ToSseDataLine(
                    ChatStreamEventContract.CreateDone(
                        fallbackUsed: true,
                        fallbackReason: fallbackReason)));
            await Response.Body.FlushAsync();
        }

        private static bool IsBillableAiResponse(AiChatResponseDto aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse.Answer))
            {
                return false;
            }

            var debugIntent = aiResponse.DebugIntent;
            if (debugIntent is null)
            {
                return true;
            }

            try
            {
                using var doc = debugIntent is JsonElement element
                    ? JsonDocument.Parse(element.GetRawText())
                    : JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(debugIntent));

                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return true;
                }

                if (root.TryGetProperty("fallback", out var fallback) &&
                    fallback.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                    fallback.GetBoolean())
                {
                    return false;
                }

                if (root.TryGetProperty("fallback_reason", out var fallbackReason) &&
                    fallbackReason.ValueKind == JsonValueKind.String)
                {
                    return !IsNonBillableFallbackReason(fallbackReason.GetString());
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsNonBillableFallbackReason(string? reason)
        {
            return reason is "ai_capacity_limited" or "ai_timeout" or "ai_upstream_error" or "ai_exception";
        }

        private static string? ReadDebugIntentString(string? debugIntentJson, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(debugIntentJson))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(debugIntentJson);
                return doc.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        [HttpPost("visual-feedback")]
        [EnableRateLimiting("public-feedback")]
        public async Task<IActionResult> SaveVisualFeedback([FromForm] VisualFeedbackFormRequest request)
        {
            var validationError = UploadValidation.ValidateImage(request.Image, required: true);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new { success = false, message = validationError });
            }

            try
            {
                var userResult = await _sender.Send(new ResolveChatUserQuery(GetCurrentUserId(), request.PublicToken));
                if (!userResult.IsSuccess)
                {
                    return BadRequest(new { success = false, message = userResult.ErrorMessage ?? "Geçerli kullanıcı veya public token gerekli." });
                }

                var userId = userResult.Value!.UserId;

                byte[] imageBytes = [];
                string fileName = "image.jpg";
                string contentType = "image/jpeg";

                if (request.Image != null)
                {
                    await using var ms = new MemoryStream();
                    await request.Image.CopyToAsync(ms, HttpContext.RequestAborted);
                    imageBytes = ms.ToArray();
                    fileName = request.Image.FileName;
                    contentType = request.Image.ContentType ?? contentType;
                }

                var result = await _sender.Send(new SaveVisualFeedbackCommand(
                    userId,
                    imageBytes,
                    fileName,
                    contentType,
                    request.PartName,
                    request.PartCode,
                    request.MachineBrand,
                    request.MachineType,
                    request.Note));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(new { success = false, message = result.ErrorMessage }),
                        _ => StatusCode(500, new { success = false, message = result.ErrorMessage ?? "Sistem hatası oluştu." })
                    };
                }

                return result.Value!.Success
                    ? Ok(result.Value)
                    : BadRequest(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Visual feedback kaydetme hatası.");
                return StatusCode(500, new { success = false, message = "Sistem hatası oluştu." });
            }
        }

        [HttpPost("feedback")]
        [EnableRateLimiting("public-feedback")]
        public async Task<IActionResult> SaveChatFeedback([FromBody] ChatFeedbackRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { success = false, message = "Geçersiz geri bildirim verisi." });

                if (string.IsNullOrWhiteSpace(request.ReplySuggestion))
                    return BadRequest(new { success = false, message = "replySuggestion zorunludur." });

                var userResult = await _sender.Send(new ResolveChatUserQuery(GetCurrentUserId(), request.PublicToken));
                if (!userResult.IsSuccess)
                {
                    return BadRequest(new { success = false, message = userResult.ErrorMessage ?? "Geçerli kullanıcı veya public token gerekli." });
                }

                var userId = userResult.Value!.UserId;
                var isPublic = userResult.Value.IsPublic;

                var result = await _sender.Send(new SaveChatFeedbackCommand(
                    userId,
                    isPublic,
                    request.Helpful,
                    request.Reason,
                    request.UserQuery,
                    request.ReplySuggestion,
                    request.SourceCodes,
                    request.CatalogIds,
                    request.PublicToken,
                    request.ContextJson,
                    request.MessageId,
                    request.ConversationId,
                    Request.Headers.UserAgent.ToString(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(new { success = false, message = result.ErrorMessage }),
                        _ => StatusCode(500, new { success = false, message = result.ErrorMessage ?? "Geri bildirim kaydedilemedi." })
                    };
                }

                return Ok(new { success = true, id = result.Value!.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat feedback kaydetme hatası.");
                return StatusCode(500, new { success = false, message = "Sistem hatası oluştu." });
            }
        }

    }

    public sealed class VisualFeedbackFormRequest
    {
        public IFormFile? Image { get; set; }
        public string? PartName { get; set; }
        public string? PartCode { get; set; }
        public string? MachineBrand { get; set; }
        public string? MachineType { get; set; }
        public string? PublicToken { get; set; }
        public string? Note { get; set; }
    }
}
