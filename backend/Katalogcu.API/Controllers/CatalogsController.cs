using Katalogcu.API.Services;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Commands.ClearCatalogPageData;
using Katalogcu.Application.Features.Catalogs.Commands.CompleteCatalogUpload;
using Katalogcu.Application.Features.Catalogs.Commands.CreateCatalog;
using Katalogcu.Application.Features.Catalogs.Commands.DeleteCatalog;
using Katalogcu.Application.Features.Catalogs.Commands.FailCatalogUpload;
using Katalogcu.Application.Features.Catalogs.Commands.MoveCatalog;
using Katalogcu.Application.Features.Catalogs.Commands.PublishCatalog;
using Katalogcu.Application.Features.Catalogs.Commands.RevokePublicToken;
using Katalogcu.Application.Features.Catalogs.Commands.RotatePublicToken;
using Katalogcu.Application.Features.Catalogs.Commands.StartCatalogAiProcess;
using Katalogcu.Application.Features.Catalogs.Commands.TrackCatalogView;
using Katalogcu.Application.Features.Catalogs.Commands.TrackStorefrontView;
using Katalogcu.Application.Features.Catalogs.Queries.GetCatalogById;
using Katalogcu.Application.Features.Catalogs.Queries.GetCatalogPageItems;
using Katalogcu.Application.Features.Catalogs.Queries.GetCatalogAiJobs;
using Katalogcu.Application.Features.Catalogs.Queries.GetCatalogStats;
using Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogs;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogs;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogsByUser;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicStorefront;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicToken;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicTokenStatus;
using Katalogcu.Application.Features.Folders.Queries.GetPublicFoldersByUser;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Katalogcu.Infrastructure.Persistence;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")] // 🔒 Varsayılan: Yönetim paneli kullanıcıları
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogsController : ControllerBase
    {
        private readonly ILogger<CatalogsController> _logger;
        private readonly IPublicAccessTokenService _publicAccessTokenService;
        private readonly IAiUsageQuotaService _aiUsageQuotaService;
        private readonly IProductFeaturePolicy _productFeaturePolicy;
        private readonly AppDbContext _dbContext;
        private readonly ISender _sender;

        public CatalogsController(
            ILogger<CatalogsController> logger,
            IPublicAccessTokenService publicAccessTokenService,
            IAiUsageQuotaService aiUsageQuotaService,
            IProductFeaturePolicy productFeaturePolicy,
            AppDbContext dbContext,
            ISender sender)
        {
            _logger = logger;
            _publicAccessTokenService = publicAccessTokenService;
            _aiUsageQuotaService = aiUsageQuotaService;
            _productFeaturePolicy = productFeaturePolicy;
            _dbContext = dbContext;
            _sender = sender;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid))
            {
                return guid;
            }
            return Guid.Empty;
        }

        private (Guid userId, bool isPublic, PublicAccessPayloadDto? publicPayload) ResolveUserId(string? publicToken)
        {
            var tokenUserId = GetCurrentUserId();
            if (tokenUserId != Guid.Empty) return (tokenUserId, false, null);

            if (!string.IsNullOrWhiteSpace(publicToken))
            {
                var payload = _publicAccessTokenService.Validate(publicToken);
                if (payload != null) return (payload.UserId, true, payload);
            }
            return (Guid.Empty, true, null);
        }

        private static List<Guid> ParseCatalogIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<Guid>();
            try
            {
                var strIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? new();
                return strIds
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        // ==========================================
        // 🌍 PUBLIC VIEW (HERKESE AÇIK LİSTE)
        // ==========================================
        [AllowAnonymous] 
        [HttpGet("public")] 
        public async Task<IActionResult> GetPublicCatalogs()
        {
            var result = await _sender.Send(new GetPublicCatalogsQuery());
            if (!result.IsSuccess)
            {
                return StatusCode(500, result.ErrorMessage ?? "Public kataloglar alınamadı.");
            }

            return Ok(result.Value);
        }

        // ==========================================
        // 🌍 PUBLIC VIEW (KULLANICIYA ÖZEL)
        // ==========================================
        [AllowAnonymous]
        [HttpGet("public/{userId:guid}")]
        public IActionResult GetPublicCatalogsByUser(Guid userId)
        {
            return BadRequest("Bu endpoint devre dışı. public token kullanın.");
        }

        [AllowAnonymous]
        [HttpGet("public-by-token")]
        public async Task<IActionResult> GetPublicCatalogsByToken([FromQuery] string token)
        {
            var payload = _publicAccessTokenService.Validate(token);
            if (payload == null) return BadRequest("Geçersiz token.");

            var result = await _sender.Send(new GetPublicCatalogsByUserQuery(payload.UserId, payload.CatalogIds));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public kataloglar alınamadı.")
                };
            }

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpGet("public-storefront")]
        public async Task<IActionResult> GetPublicStorefront([FromQuery] string token)
        {
            var payload = _publicAccessTokenService.Validate(token);
            if (payload == null) return BadRequest("Geçersiz token.");

            var trackStorefrontResult = await _sender.Send(new TrackStorefrontViewCommand(
                payload.UserId,
                BuildPublicViewFingerprint(),
                DateTime.UtcNow,
                "public-storefront"));

            if (!trackStorefrontResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Storefront view track failed. ownerUserId={OwnerUserId} error={ErrorCode}",
                    payload.UserId,
                    trackStorefrontResult.ErrorCode);
            }

            var result = await _sender.Send(new GetPublicStorefrontQuery(payload.UserId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Storefront bilgisi alınamadı.")
                };
            }

            var storefront = result.Value!;
            return Ok(new
            {
                businessName = storefront.BusinessName,
                ownerName = storefront.OwnerName,
                email = storefront.Email,
                phoneNumber = storefront.PhoneNumber,
                subscriptionPlan = storefront.SubscriptionPlan,
                aiChatEnabled = storefront.AiChatEnabled && _productFeaturePolicy.ChatbotEnabled,
                ecommerceEnabled = storefront.EcommerceEnabled && _productFeaturePolicy.EcommerceEnabled
            });
        }

        [AllowAnonymous]
        [HttpGet("public-folders-by-token")]
        public async Task<IActionResult> GetPublicFoldersByToken([FromQuery] string token)
        {
            var payload = _publicAccessTokenService.Validate(token);
            if (payload == null) return BadRequest("Geçersiz token.");

            var result = await _sender.Send(new GetPublicFoldersByUserQuery(payload.UserId, payload.CatalogIds));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public klasörler alınamadı.")
                };
            }

            return Ok(result.Value);
        }

        [HttpGet("public-token")]
        public async Task<IActionResult> GetPublicToken([FromQuery] string? catalogIds)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var requestedIds = ParseCatalogIds(catalogIds);
            var result = await _sender.Send(new GetPublicTokenQuery(userId, requestedIds));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public token oluşturulamadı.")
                };
            }

            return Ok(new { token = result.Value! });
        }

        [HttpGet("public-token/status")]
        public async Task<IActionResult> GetPublicTokenStatus()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var result = await _sender.Send(new GetPublicTokenStatusQuery(userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public token durumu alınamadı.")
                };
            }

            return Ok(new
            {
                enabled = result.Value!.Enabled,
                version = result.Value.Version
            });
        }

        [HttpPost("public-token/revoke")]
        public async Task<IActionResult> RevokePublicToken()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var result = await _sender.Send(new RevokePublicTokenCommand(userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public token iptal edilemedi.")
                };
            }

            return Ok(new
            {
                enabled = result.Value!.Enabled,
                version = result.Value.Version
            });
        }

        [HttpPost("public-token/rotate")]
        public async Task<IActionResult> RotatePublicToken([FromQuery] string? catalogIds)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var requestedIds = ParseCatalogIds(catalogIds);
            var result = await _sender.Send(new RotatePublicTokenCommand(userId, requestedIds));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Public token yenilenemedi.")
                };
            }

            return Ok(new
            {
                token = result.Value!.Token,
                enabled = result.Value.Enabled,
                version = result.Value.Version
            });
        }

        // ==========================================
        // 📂 1. KATALOG TAŞIMA (KLASÖR YÖNETİMİ)
        // ==========================================
        [HttpPut("{id}/move")]
        public async Task<IActionResult> MoveCatalog(Guid id, [FromBody] MoveCatalogDto request)
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new MoveCatalogCommand(id, userId, request.FolderId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Katalog taşınamadı.")
                };
            }

            var response = result.Value!;
            return Ok(new { message = response.Message, folderId = response.FolderId });
        }

        // ==========================================
        // 🤖 2. AI İŞLEMİ (PYTHON TETİKLEYİCİLİ)
        // ==========================================
        [HttpPost("{id}/start-ai-process")]
        public async Task<IActionResult> StartAutonomousProcess(Guid id)
        {
            var userId = GetCurrentUserId();
            var commandResult = await _sender.Send(new StartCatalogAiProcessCommand(id, userId));
            if (!commandResult.IsSuccess)
            {
                return commandResult.ErrorCode switch
                {
                    "not_found" => NotFound(commandResult.ErrorMessage),
                    "validation" => BadRequest(commandResult.ErrorMessage),
                    _ => StatusCode(500, commandResult.ErrorMessage ?? "AI analizi başlatılamadı.")
                };
            }

            return Accepted(commandResult.Value);
        }

        // ==========================================
        // 📄 3. SAYFA ÖĞELERİNİ GETİR (RefNumber Uyumlu)
        // ==========================================
        [AllowAnonymous]
        [HttpGet("{id}/pages/{pageNumber}/items")]
        public async Task<IActionResult> GetPageItems(Guid id, string pageNumber, [FromQuery] string? token, [FromQuery] bool strict = false)
        {
            var resolved = ResolveUserId(token);
            if (resolved.userId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            if (!int.TryParse(pageNumber, out int currentPage)) return BadRequest("Sayfa numarası geçersiz.");

            var result = await _sender.Send(new GetCatalogPageItemsQuery(
                id,
                currentPage,
                resolved.userId,
                resolved.isPublic,
                resolved.publicPayload?.CatalogIds,
                strict));

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Sayfa öğeleri alınamadı.")
                };
            }

            return Ok(result.Value);
        }

        // ==========================================
        // STANDART CRUD İŞLEMLERİ
        // ==========================================

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new GetCatalogStatsQuery(userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "İstatistikler alınamadı.")
                };
            }

            var stats = result.Value!;
            var aiUsage = await _aiUsageQuotaService.GetCurrentUsageAsync(userId, HttpContext.RequestAborted);
            return Ok(new
            {
                TotalCatalogs = stats.TotalCatalogs,
                TotalParts = stats.TotalParts,
                TotalViews = stats.TotalViews,
                ViewsLast7Days = stats.ViewsLast7Days,
                UniqueViewersLast30Days = stats.UniqueViewersLast30Days,
                StorefrontVisitsTotal = stats.StorefrontVisitsTotal,
                StorefrontVisitsToday = stats.StorefrontVisitsToday,
                StorefrontVisitsLast7Days = stats.StorefrontVisitsLast7Days,
                StorefrontUniqueVisitorsLast30Days = stats.StorefrontUniqueVisitorsLast30Days,
                PendingCount = stats.PendingCount,
                RecentCatalogs = stats.RecentCatalogs,
                TopViewedCatalogs = stats.TopViewedCatalogs,
                VisualEmbeddingCount = stats.VisualEmbeddingCount,
                AiUsedThisMonth = aiUsage.UsedThisMonth,
                AiMonthlyLimit = aiUsage.MonthlyLimit,
                AiRemainingThisMonth = aiUsage.RemainingThisMonth,
                AiEnabled = aiUsage.AiEnabled,
                AiUnlimited = aiUsage.Unlimited
            });
        }

        [HttpGet("ai-jobs")]
        public async Task<IActionResult> GetCatalogAiJobs([FromQuery] int take = 50)
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new GetCatalogAiJobsQuery(userId, take));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "AI job listesi alınamadı.")
                };
            }

            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new GetMyCatalogsQuery(userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Kataloglar alınamadı.")
                };
            }

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] string? token)
        {
            var resolved = ResolveUserId(token);
            if (resolved.userId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            var result = await _sender.Send(new GetCatalogByIdQuery(
                id,
                resolved.userId,
                resolved.isPublic,
                resolved.publicPayload?.CatalogIds));

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "not_found" => NotFound("Katalog bulunamadı."),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Katalog alınamadı.")
                };
            }

            if (resolved.isPublic)
            {
                var trackViewResult = await _sender.Send(new TrackCatalogViewCommand(
                    id,
                    resolved.userId,
                    BuildPublicViewFingerprint(),
                    DateTime.UtcNow,
                    "public-view"));

                if (!trackViewResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Catalog view track failed. catalogId={CatalogId} userId={UserId} error={ErrorCode}",
                        id,
                        resolved.userId,
                        trackViewResult.ErrorCode);
                }
            }

            return Ok(result.Value);
        }

        private string BuildPublicViewFingerprint()
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
            var ip = !string.IsNullOrWhiteSpace(forwardedFor)
                ? forwardedFor.Split(',')[0].Trim()
                : HttpContext.Connection.RemoteIpAddress?.ToString();

            var userAgent = Request.Headers.UserAgent.ToString();
            var acceptLanguage = Request.Headers.AcceptLanguage.ToString();

            var rawFingerprint = $"{ip ?? "unknown"}|{userAgent}|{acceptLanguage}";
            var bytes = Encoding.UTF8.GetBytes(rawFingerprint);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCatalogRequest request)
        {
            var userId = GetCurrentUserId();
            var createResult = await _sender.Send(new CreateCatalogCommand(
                userId,
                request.Name ?? string.Empty,
                request.Description,
                request.PdfUrl,
                request.ImageUrl,
                request.FolderId));

            if (!createResult.IsSuccess)
            {
                return createResult.ErrorCode switch
                {
                    "validation" => BadRequest(createResult.ErrorMessage),
                    _ => StatusCode(500, createResult.ErrorMessage ?? "Katalog oluşturulamadı.")
                };
            }

            var createdCatalog = createResult.Value!;

            if (!string.IsNullOrEmpty(createdCatalog.PdfUrl))
            {
                try
                {
                    var completeResult = await _sender.Send(new CompleteCatalogUploadCommand(
                        createdCatalog.Id,
                        userId));

                    if (!completeResult.IsSuccess)
                    {
                        await _sender.Send(new FailCatalogUploadCommand(createdCatalog.Id, userId));
                        return completeResult.ErrorCode switch
                        {
                            "not_found" => NotFound(completeResult.ErrorMessage),
                            "validation" => BadRequest(completeResult.ErrorMessage),
                            _ => StatusCode(500, completeResult.ErrorMessage ?? "PDF işlenirken hata oluştu.")
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PDF işleme hatası");
                    await _sender.Send(new FailCatalogUploadCommand(createdCatalog.Id, userId));
                    return StatusCode(500, "PDF işlenirken hata oluştu.");
                }
            }
            return CreatedAtAction(nameof(GetById), new { id = createdCatalog.Id }, createdCatalog);
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new PublishCatalogCommand(id, userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "not_found" => NotFound(),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Yayınlama başarısız.")
                };
            }

            var response = result.Value!;
            return Ok(new { message = response.Message, status = response.Status });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();
            try
            {
                var result = await _sender.Send(new DeleteCatalogCommand(id, userId));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Silme işlemi sırasında hata oluştu.")
                    };
                }

                return NoContent();
            }
            catch (FluentValidation.ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silme işlemi hatası");
                return StatusCode(500, "Silme işlemi sırasında beklenmeyen bir hata oluştu.");
            }
        }

        [HttpDelete("{id}/pages/{pageId}/clear")]
        public async Task<IActionResult> ClearPageData(Guid id, Guid pageId)
        {
            var userId = GetCurrentUserId();
            var result = await _sender.Send(new ClearCatalogPageDataCommand(id, pageId, userId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Sayfa temizlenemedi.")
                };
            }

            return Ok(new { message = result.Value!.Message });
        }

        [HttpPut("{id}/pages/{pageId}/review")]
        public async Task<IActionResult> UpdatePageReview(Guid id, Guid pageId, [FromBody] UpdateCatalogPageReviewRequest request)
        {
            var userId = GetCurrentUserId();
            var normalizedStatus = NormalizeReviewStatus(request.ReviewStatus);
            if (normalizedStatus == null)
            {
                return BadRequest("Geçersiz review durumu.");
            }

            var page = await _dbContext.CatalogPages
                .Include(p => p.Catalog)
                .FirstOrDefaultAsync(p => p.Id == pageId && p.CatalogId == id, HttpContext.RequestAborted);

            if (page == null || page.Catalog?.UserId != userId)
            {
                return NotFound("Sayfa bulunamadı.");
            }

            page.ReviewStatus = normalizedStatus;
            page.ReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes)
                ? null
                : request.ReviewNotes.Trim();
            page.ReviewedAt = normalizedStatus == "Reviewed" ? DateTime.UtcNow : null;
            page.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                reviewStatus = page.ReviewStatus,
                reviewNotes = page.ReviewNotes,
                reviewedAt = page.ReviewedAt,
                updatedDate = page.UpdatedDate
            });
        }

        private static string? NormalizeReviewStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim().ToLowerInvariant() switch
            {
                "needsreview" => "NeedsReview",
                "reviewed" => "Reviewed",
                _ => null
            };
        }
    }

    // --- DTO ---
    public class MoveCatalogDto
    {
        public Guid? FolderId { get; set; }
    }

    public sealed class CreateCatalogRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? PdfUrl { get; set; }
        public Guid? FolderId { get; set; }
    }

    public sealed class UpdateCatalogPageReviewRequest
    {
        [Required]
        public string ReviewStatus { get; set; } = string.Empty;
        [MaxLength(1024)]
        public string? ReviewNotes { get; set; }
    }
}
