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

        public ChatController(
            IPartalogAiService aiService,
            ISender sender,
            ILogger<ChatController> logger,
            IChatStreamProxyService chatStreamProxyService)
        {
            _aiService = aiService;
            _sender = sender;
            _logger = logger;
            _chatStreamProxyService = chatStreamProxyService;
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
        [EnableRateLimiting("public-chat")]
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
                    Image = request.Image,
                    History = chatHistory,
                    CatalogIds = catalogIdStrings
                };

                // 3. AI Analizi (Python)
                var aiResponse = await _aiService.GetExpertChatResponseAsync(aiRequest);

                var sourceInputs = (aiResponse.Sources ?? new List<ChatSourceDto>())
                    .Select(source => new ChatSourceInput
                    {
                        Code = source.Code,
                        Name = source.Name,
                        Model = source.Model,
                        LegacyModel = source.LegacyModel,
                        Description = source.Description,
                        LegacyDescription = source.LegacyDescription,
                        Query = source.Query
                    })
                    .ToList();

                var debugIntentJson = aiResponse.DebugIntent switch
                {
                    JsonElement je => je.GetRawText(),
                    null => null,
                    _ => System.Text.Json.JsonSerializer.Serialize(aiResponse.DebugIntent)
                };

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
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat Controller Hatası");
                return StatusCode(500, new { error = "Sistem hatası: " + ex.Message });
            }
        }

        [HttpPost("ask-stream")]
        [EnableRateLimiting("public-chat")]
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
            try
            {
                await _chatStreamProxyService.ProxyAskStreamAsync(
                    Response,
                    request.Text,
                    request.History,
                    catalogIdStrings,
                    request.Image,
                    HttpContext.RequestAborted);
            }
            catch
            {
                // Hata logu servis tarafında tutuluyor.
            }
        }

        [HttpPost("visual-feedback")]
        [EnableRateLimiting("public-feedback")]
        public async Task<IActionResult> SaveVisualFeedback([FromForm] VisualFeedbackRequestDto request)
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
}
