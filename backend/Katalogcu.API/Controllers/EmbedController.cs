using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.API.Services;
using Katalogcu.Domain.Entities;
using Katalogcu.Domain.Enums;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/embed")]
public class EmbedController : ControllerBase
{
    private readonly IEmbedOriginService _embedOriginService;
    private readonly IPublicAccessTokenService _publicAccessTokenService;
    private readonly IEmbedAnalyticsService _embedAnalyticsService;
    private readonly IEmbedDomainVerificationService _embedDomainVerificationService;
    private readonly IPublicCatalogLinkService _publicCatalogLinkService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public EmbedController(
        IEmbedOriginService embedOriginService,
        IPublicAccessTokenService publicAccessTokenService,
        IEmbedAnalyticsService embedAnalyticsService,
        IEmbedDomainVerificationService embedDomainVerificationService,
        IPublicCatalogLinkService publicCatalogLinkService,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _embedOriginService = embedOriginService;
        _publicAccessTokenService = publicAccessTokenService;
        _embedAnalyticsService = embedAnalyticsService;
        _embedDomainVerificationService = embedDomainVerificationService;
        _publicCatalogLinkService = publicCatalogLinkService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var storeSlug = await EnsureUserStoreSlugAsync(userId, cancellationToken);
        var settings = await _embedOriginService.GetOrCreateAsync(userId, cancellationToken);
        return Ok(new
        {
            userId = settings.UserId,
            allowedOrigins = settings.AllowedOrigins,
            theme = settings.Theme,
            mode = settings.Mode,
            storeSlug
        });
    }

    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateEmbedSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var storeSlug = await UpsertStoreSlugAsync(userId, request.StoreSlug, cancellationToken);
            var updated = await _embedOriginService.UpsertAsync(
                userId,
                request.AllowedOrigins ?? [],
                request.Theme,
                request.Mode,
                cancellationToken);

            return Ok(new
            {
                userId = updated.UserId,
                allowedOrigins = updated.AllowedOrigins,
                theme = updated.Theme,
                mode = updated.Mode,
                storeSlug
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("verify-origin")]
    [AllowAnonymous]
    [EnableRateLimiting("public-embed-events")]
    public async Task<IActionResult> VerifyOrigin([FromBody] VerifyOriginRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.EmbedKey))
        {
            var resolvedTarget = await ResolveTargetResponseAsync(request.EmbedKey, request.Origin, cancellationToken);
            if (resolvedTarget == null)
            {
                return BadRequest(new { allowed = false, reason = "invalid_embed_key" });
            }

            return Ok(resolvedTarget);
        }

        if (string.IsNullOrWhiteSpace(request.PublicToken) && string.IsNullOrWhiteSpace(request.StoreSlug))
        {
            return BadRequest(new { allowed = false, reason = "token_or_store_required" });
        }

        var resolved = await ResolveEmbedTargetAsync(request.PublicToken, request.StoreSlug, cancellationToken);
        if (resolved == null)
        {
            return BadRequest(new { allowed = false, reason = string.IsNullOrWhiteSpace(request.StoreSlug) ? "invalid_token" : "invalid_store" });
        }

        var rawOrigin = string.IsNullOrWhiteSpace(request.Origin)
            ? HttpContext.Request.Headers.Origin.ToString()
            : request.Origin;

        var normalizedOrigin = _embedOriginService.NormalizeOrigin(rawOrigin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            return BadRequest(new { allowed = false, reason = "origin_required" });
        }

        var settings = await _embedOriginService.GetOrCreateAsync(resolved.UserId, cancellationToken);
        var allowed = settings.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);

        return Ok(new
        {
            allowed,
            reason = allowed ? "ok" : "origin_not_allowed",
            origin = normalizedOrigin,
            ownerUserId = resolved.UserId,
            theme = settings.Theme,
            mode = settings.Mode,
            whiteLabel = resolved.Plan == SubscriptionPlan.CatalogWithAIAndEcommerce,
            publicToken = resolved.PublicToken,
            storeSlug = resolved.StoreSlug,
            appBaseUrl = ResolveAppBaseUrl()
        });
    }

    [HttpPost("events")]
    [AllowAnonymous]
    [EnableRateLimiting("public-embed-events")]
    public async Task<IActionResult> IngestEvent([FromBody] EmbedEventIngestRequest request, CancellationToken cancellationToken)
    {
        var token = ResolvePublicToken(request.PublicToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { success = false, reason = "token_required" });
        }

        var payload = _publicAccessTokenService.Validate(token);
        if (payload == null || payload.UserId == Guid.Empty)
        {
            return BadRequest(new { success = false, reason = "invalid_token" });
        }

        var eventName = NormalizeEventName(request.EventName);
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return BadRequest(new { success = false, reason = "event_required" });
        }

        if (!IsAllowedEvent(eventName))
        {
            return BadRequest(new { success = false, reason = "event_not_supported" });
        }

        var normalizedOrigin = _embedOriginService.NormalizeOrigin(HttpContext.Request.Headers.Origin.ToString());
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            return BadRequest(new { success = false, reason = "origin_required" });
        }

        var isAllowed = await _embedOriginService.IsOriginAllowedAsync(payload.UserId, normalizedOrigin, cancellationToken);
        if (!isAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, reason = "origin_not_allowed" });
        }

        var fingerprint = _embedAnalyticsService.BuildFingerprint(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            HttpContext.Request.Headers.AcceptLanguage.ToString());

        await _embedAnalyticsService.IngestAsync(
            payload.UserId,
            eventName,
            string.IsNullOrWhiteSpace(request.Source) ? "sdk-v1" : request.Source.Trim(),
            fingerprint,
            normalizedOrigin,
            request.PageUrl?.Trim(),
            request.Payload.ValueKind == JsonValueKind.Undefined ? null : request.Payload.GetRawText(),
            cancellationToken);

        return Ok(new { success = true });
    }

    [HttpGet("domains")]
    [Authorize]
    public async Task<IActionResult> GetDomains(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var rows = await _embedDomainVerificationService.GetByUserAsync(userId, cancellationToken);
        return Ok(rows.Select(MapDomainRow));
    }

    [HttpPost("domains/challenge")]
    [Authorize]
    public async Task<IActionResult> CreateDomainChallenge([FromBody] CreateDomainChallengeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var row = await _embedDomainVerificationService.CreateChallengeAsync(userId, request.Origin, request.Method, cancellationToken);
            return Ok(MapDomainRow(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("store-slug/check")]
    [Authorize]
    public async Task<IActionResult> CheckStoreSlug([FromQuery] string? slug, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.CompanyName,
                u.FirstName,
                u.LastName,
                u.PublicStoreSlug
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return NotFound(new { success = false, message = "Kullanıcı bulunamadı." });
        }

        var normalized = NormalizeStoreSlug(slug);
        var suggested = string.IsNullOrWhiteSpace(normalized)
            ? await BuildUniqueStoreSlugAsync(user.CompanyName, $"{user.FirstName} {user.LastName}", user.Id, cancellationToken)
            : normalized;

        var takenByAnother = !string.IsNullOrWhiteSpace(normalized) && await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id != userId && u.PublicStoreSlug == normalized, cancellationToken);

        return Ok(new
        {
            normalized,
            current = user.PublicStoreSlug,
            available = string.IsNullOrWhiteSpace(normalized) || !takenByAnother || string.Equals(user.PublicStoreSlug, normalized, StringComparison.Ordinal),
            suggested = takenByAnother
                ? await BuildUniqueStoreSlugAsync(normalized, normalized, user.Id, cancellationToken)
                : suggested
        });
    }

    [HttpPost("domains/{id:guid}/verify-now")]
    [Authorize]
    public async Task<IActionResult> VerifyDomainNow(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var row = await _embedDomainVerificationService.VerifyNowAsync(userId, id, cancellationToken);
        if (row == null) return NotFound(new { success = false, message = "Kayıt bulunamadı." });
        return Ok(MapDomainRow(row));
    }

    [HttpPost("domains/{id:guid}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateVerifiedDomain(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var row = await _embedDomainVerificationService.ActivateOriginAsync(userId, id, cancellationToken);
            if (row == null) return NotFound(new { success = false, message = "Kayıt bulunamadı." });
            return Ok(MapDomainRow(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("domains/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteDomainVerification(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var deleted = await _embedDomainVerificationService.DeleteAsync(userId, id, cancellationToken);
        if (!deleted) return NotFound(new { success = false, message = "Kayıt bulunamadı." });
        return Ok(new { success = true });
    }

    [HttpGet("targets")]
    [Authorize]
    public async Task<IActionResult> GetEmbedTargets(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var targets = await _dbContext.EmbedTargets
            .AsNoTracking()
            .Include(x => x.Catalog)
            .Include(x => x.CatalogPage)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                type = x.Type,
                catalogId = x.CatalogId,
                catalogName = x.Catalog != null ? x.Catalog.Name : string.Empty,
                catalogPageId = x.CatalogPageId,
                pageNumber = x.CatalogPage != null ? x.CatalogPage.PageNumber : (int?)null,
                commerceMode = x.CommerceMode,
                hostActionMode = x.HostActionMode,
                productUrlTemplate = x.ProductUrlTemplate,
                searchUrlTemplate = x.SearchUrlTemplate,
                existingCartUrl = x.ExistingCartUrl,
                existingCartMethod = x.ExistingCartMethod,
                accessExpiresAt = x.AccessExpiresAt,
                isActive = x.IsActive,
                embedKey = x.EmbedKey,
                createdDate = x.CreatedDate,
                updatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return Ok(targets);
    }

    [HttpPost("targets")]
    [Authorize]
    public async Task<IActionResult> CreateEmbedTarget([FromBody] CreateEmbedTargetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var prepared = await PrepareEmbedTargetAsync(userId, null, request, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return BadRequest(new { success = false, message = prepared.ErrorMessage });
        }

        var entity = prepared.Value!;
        await _dbContext.EmbedTargets.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await MapEmbedTargetResponseAsync(entity.Id, userId, cancellationToken));
    }

    [HttpPut("targets/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateEmbedTarget(Guid id, [FromBody] CreateEmbedTargetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var existing = await _dbContext.EmbedTargets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (existing == null)
        {
            return NotFound(new { success = false, message = "Embed kaydı bulunamadı." });
        }

        var prepared = await PrepareEmbedTargetAsync(userId, existing, request, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return BadRequest(new { success = false, message = prepared.ErrorMessage });
        }

        existing.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await MapEmbedTargetResponseAsync(existing.Id, userId, cancellationToken));
    }

    [HttpDelete("targets/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteEmbedTarget(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var existing = await _dbContext.EmbedTargets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (existing == null)
        {
            return NotFound(new { success = false, message = "Embed kaydı bulunamadı." });
        }

        _dbContext.EmbedTargets.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("resolve-target")]
    [AllowAnonymous]
    [EnableRateLimiting("public-embed-events")]
    public async Task<IActionResult> ResolveTarget([FromBody] ResolveEmbedTargetRequest request, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTargetResponseAsync(request.EmbedKey, request.Origin, cancellationToken);
        if (resolved == null)
        {
            return BadRequest(new { allowed = false, reason = "invalid_embed_key" });
        }

        return Ok(resolved);
    }

    [HttpGet("config/{embedKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEmbedConfig(string embedKey, CancellationToken cancellationToken)
    {
        var config = await BuildEmbedConfigAsync(embedKey, cancellationToken);
        if (config == null)
        {
            return NotFound(new { success = false, message = "Embed kaydı bulunamadı." });
        }

        return Ok(new
        {
            ownerUserId = config.OwnerUserId,
            whiteLabel = config.WhiteLabel,
            embedKey = config.EmbedKey,
            targetType = config.TargetType,
            commerceMode = config.CommerceMode,
            hostActionMode = config.HostActionMode,
            catalogId = config.CatalogId,
            catalogPageId = config.CatalogPageId,
            pageNumber = config.PageNumber,
            pageIndex = config.PageIndex,
            publicToken = config.PublicToken,
            embedTokenExpiresAtUtc = config.EmbedTokenExpiresAtUtc,
            theme = config.Theme,
            mode = config.Mode,
            productUrlTemplate = config.ProductUrlTemplate,
            searchUrlTemplate = config.SearchUrlTemplate,
            existingCartUrl = config.ExistingCartUrl,
            existingCartMethod = config.ExistingCartMethod,
            accessExpiresAtUtc = config.AccessExpiresAtUtc,
            runtimePath = config.RuntimePath
        });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId) && userId != Guid.Empty;
    }

    private string? ResolvePublicToken(string? bodyToken)
    {
        if (!string.IsNullOrWhiteSpace(bodyToken))
        {
            return bodyToken.Trim();
        }

        var fromHeader = HttpContext.Request.Headers["X-Public-Token"].ToString();
        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            return fromHeader.Trim();
        }

        var fromQueryToken = HttpContext.Request.Query["token"].ToString();
        if (!string.IsNullOrWhiteSpace(fromQueryToken))
        {
            return fromQueryToken.Trim();
        }

        var fromQueryPublicToken = HttpContext.Request.Query["publicToken"].ToString();
        if (!string.IsNullOrWhiteSpace(fromQueryPublicToken))
        {
            return fromQueryPublicToken.Trim();
        }

        return null;
    }

    private static string NormalizeEventName(string? eventName)
    {
        return string.IsNullOrWhiteSpace(eventName) ? string.Empty : eventName.Trim().ToLowerInvariant();
    }

    private static bool IsAllowedEvent(string eventName)
    {
        return eventName is "part:viewed"
            or "cart:add"
            or "checkout:start"
            or "part:add-to-cart"
            or "part:availability-request"
            or "part:view-product"
            or "part:search";
    }

    private static object MapDomainRow(EmbedDomainVerificationDto row)
    {
        var instructions = row.Method == "dns_txt"
            ? new
            {
                type = "dns_txt",
                recordName = $"_partalog-challenge.{row.Domain}",
                recordType = "TXT",
                recordValue = row.ChallengeToken,
                filePath = (string?)null,
                fileUrl = (string?)null,
                fileContent = (string?)null
            }
            : new
            {
                type = "file",
                recordName = (string?)null,
                recordType = (string?)null,
                recordValue = (string?)null,
                filePath = "/.well-known/partalog-verification.txt",
                fileUrl = $"{row.Origin}/.well-known/partalog-verification.txt",
                fileContent = row.ChallengeToken
            };

        return new
        {
            id = row.Id,
            userId = row.UserId,
            origin = row.Origin,
            domain = row.Domain,
            method = row.Method,
            status = row.Status,
            challengeToken = row.ChallengeToken,
            verifiedAt = row.VerifiedAt,
            lastError = row.LastError,
            instructions
        };
    }

    private async Task<object?> MapEmbedTargetResponseAsync(Guid targetId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.EmbedTargets
            .AsNoTracking()
            .Include(x => x.Catalog)
            .Include(x => x.CatalogPage)
            .Where(x => x.Id == targetId && x.UserId == userId)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                type = x.Type,
                catalogId = x.CatalogId,
                catalogName = x.Catalog != null ? x.Catalog.Name : string.Empty,
                catalogPageId = x.CatalogPageId,
                pageNumber = x.CatalogPage != null ? x.CatalogPage.PageNumber : (int?)null,
                commerceMode = x.CommerceMode,
                hostActionMode = x.HostActionMode,
                productUrlTemplate = x.ProductUrlTemplate,
                searchUrlTemplate = x.SearchUrlTemplate,
                existingCartUrl = x.ExistingCartUrl,
                existingCartMethod = x.ExistingCartMethod,
                isActive = x.IsActive,
                embedKey = x.EmbedKey,
                createdDate = x.CreatedDate,
                updatedDate = x.UpdatedDate
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<EmbedTargetPrepareResult> PrepareEmbedTargetAsync(
        Guid userId,
        EmbedTarget? entity,
        CreateEmbedTargetRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeEmbedTargetType(request.Type);
        if (normalizedType == null)
        {
            return EmbedTargetPrepareResult.Fail("Embed türü geçersiz. Sadece `catalog` veya `catalog_page` kullanın.");
        }

        var normalizedCommerceMode = NormalizeCommerceMode(request.CommerceMode);
        if (normalizedCommerceMode == null)
        {
            return EmbedTargetPrepareResult.Fail("Commerce modu geçersiz.");
        }

        var normalizedHostActionMode = NormalizeHostActionMode(request.HostActionMode, normalizedCommerceMode);
        if (normalizedHostActionMode == null)
        {
            return EmbedTargetPrepareResult.Fail("Host aksiyon modu geçersiz.");
        }

        var normalizedProductUrlTemplate = NormalizeOptionalText(request.ProductUrlTemplate, 1024);
        var normalizedSearchUrlTemplate = NormalizeOptionalText(request.SearchUrlTemplate, 1024);
        var normalizedExistingCartUrl = NormalizeOptionalText(request.ExistingCartUrl, 1024);
        var normalizedExistingCartMethod = NormalizeCartMethod(request.ExistingCartMethod);

        var hostActionValidationError = ValidateHostActionSettings(
            normalizedCommerceMode,
            normalizedHostActionMode,
            normalizedProductUrlTemplate,
            normalizedSearchUrlTemplate,
            normalizedExistingCartUrl);

        if (hostActionValidationError != null)
        {
            return EmbedTargetPrepareResult.Fail(hostActionValidationError);
        }

        if (request.CatalogId == Guid.Empty)
        {
            return EmbedTargetPrepareResult.Fail("Katalog seçimi zorunlu.");
        }

        var catalog = await _dbContext.Catalogs
            .AsNoTracking()
            .Where(x => x.Id == request.CatalogId && x.UserId == userId)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (catalog == null)
        {
            return EmbedTargetPrepareResult.Fail("Seçilen katalog bulunamadı.");
        }

        CatalogPage? page = null;
        if (normalizedType == "catalog_page")
        {
            if (!request.CatalogPageId.HasValue || request.CatalogPageId.Value == Guid.Empty)
            {
                return EmbedTargetPrepareResult.Fail("Tek sayfa embed için sayfa seçimi zorunlu.");
            }

            page = await _dbContext.CatalogPages
                .AsNoTracking()
                .Where(x => x.Id == request.CatalogPageId.Value && x.CatalogId == catalog.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (page == null)
            {
                return EmbedTargetPrepareResult.Fail("Seçilen katalog sayfası bulunamadı.");
            }
        }

        var safeName = string.IsNullOrWhiteSpace(request.Name)
            ? (page == null ? catalog.Name : $"{catalog.Name} - Sayfa {page.PageNumber}")
            : request.Name.Trim();

        entity ??= new EmbedTarget
        {
            UserId = userId,
            EmbedKey = GenerateEmbedKey()
        };

        entity.Name = safeName.Length > 160 ? safeName[..160].Trim() : safeName;
        entity.Type = normalizedType;
        entity.CatalogId = catalog.Id;
        entity.CatalogPageId = page?.Id;
        entity.CommerceMode = normalizedCommerceMode;
        entity.HostActionMode = normalizedHostActionMode;
        entity.ProductUrlTemplate = normalizedProductUrlTemplate;
        entity.SearchUrlTemplate = normalizedSearchUrlTemplate;
        entity.ExistingCartUrl = normalizedExistingCartUrl;
        entity.ExistingCartMethod = normalizedExistingCartMethod;
        entity.IsActive = request.IsActive ?? true;
        entity.AccessExpiresAt = request.AccessExpiresAt?.ToUniversalTime();

        return EmbedTargetPrepareResult.Ok(entity);
    }

    private async Task<object?> ResolveTargetResponseAsync(string? embedKey, string? rawOrigin, CancellationToken cancellationToken)
    {
        var config = await BuildEmbedConfigAsync(embedKey, cancellationToken);
        if (config == null)
        {
            return null;
        }

        var normalizedOrigin = _embedOriginService.NormalizeOrigin(
            string.IsNullOrWhiteSpace(rawOrigin) ? HttpContext.Request.Headers.Origin.ToString() : rawOrigin);

        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            return new
            {
                allowed = false,
                reason = "origin_required",
                embedKey = config.EmbedKey
            };
        }

        var isAllowed = await _embedOriginService.IsOriginAllowedAsync(config.OwnerUserId, normalizedOrigin, cancellationToken);
        return new
        {
            allowed = isAllowed,
            reason = isAllowed ? "ok" : "origin_not_allowed",
            origin = normalizedOrigin,
            ownerUserId = config.OwnerUserId,
            theme = config.Theme,
            mode = config.Mode,
            whiteLabel = config.WhiteLabel,
            publicToken = config.PublicToken,
            embedTokenExpiresAtUtc = config.EmbedTokenExpiresAtUtc,
            appBaseUrl = ResolveAppBaseUrl(),
            embedKey = config.EmbedKey,
            targetType = config.TargetType,
            catalogId = config.CatalogId,
            catalogPageId = config.CatalogPageId,
            pageNumber = config.PageNumber,
            pageIndex = config.PageIndex,
            commerceMode = config.CommerceMode,
            hostActionMode = config.HostActionMode,
            productUrlTemplate = config.ProductUrlTemplate,
            searchUrlTemplate = config.SearchUrlTemplate,
            existingCartUrl = config.ExistingCartUrl,
            existingCartMethod = config.ExistingCartMethod,
            accessExpiresAtUtc = config.AccessExpiresAtUtc,
            runtimePath = config.RuntimePath
        };
    }

    private async Task<EmbedResolvedConfig?> BuildEmbedConfigAsync(string? embedKey, CancellationToken cancellationToken)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(embedKey) ? null : embedKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        var target = await _dbContext.EmbedTargets
            .AsNoTracking()
            .Include(x => x.Catalog)
            .Include(x => x.CatalogPage)
            .Where(x => x.EmbedKey == normalizedKey && x.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

        if (target == null || target.Catalog == null)
        {
            return null;
        }

        var owner = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == target.UserId)
            .Select(x => new { x.Id, x.PublicStoreSlug, x.SubscriptionPlan, x.PublicLinkEnabled, x.PublicLinkVersion, x.PlanExpiresAt, x.Role })
            .SingleOrDefaultAsync(cancellationToken);

        if (owner == null || !owner.PublicLinkEnabled)
        {
            return null;
        }

        if (string.Equals(owner.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        if (owner.PlanExpiresAt.HasValue && owner.PlanExpiresAt.Value <= nowUtc)
        {
            return null;
        }

        if (target.AccessExpiresAt.HasValue && target.AccessExpiresAt.Value <= nowUtc)
        {
            return null;
        }

        var settings = await _embedOriginService.GetOrCreateAsync(target.UserId, cancellationToken);
        var embedTokenExpiresAtUtc = nowUtc.AddMinutes(GetEmbedSessionExpirationMinutes());
        var publicToken = _publicAccessTokenService.CreateEmbedSessionToken(target.UserId, [target.CatalogId], target.EmbedKey, embedTokenExpiresAtUtc);

        var pageIndex = 0;
        var pageNumber = target.CatalogPage?.PageNumber;
        if (target.CatalogPageId.HasValue)
        {
            var orderedPages = await _dbContext.CatalogPages
                .AsNoTracking()
                .Where(x => x.CatalogId == target.CatalogId)
                .OrderBy(x => x.PageNumber)
                .Select(x => new { x.Id, x.PageNumber })
                .ToListAsync(cancellationToken);

            var matchedIndex = orderedPages.FindIndex(x => x.Id == target.CatalogPageId.Value);
            if (matchedIndex >= 0)
            {
                pageIndex = matchedIndex;
                pageNumber = orderedPages[matchedIndex].PageNumber;
            }
        }

        return new EmbedResolvedConfig(
            target.UserId,
            owner.SubscriptionPlan == SubscriptionPlan.CatalogWithAIAndEcommerce,
            target.EmbedKey,
            target.Type,
            target.CommerceMode,
            target.HostActionMode,
            target.CatalogId,
            target.CatalogPageId,
            pageNumber,
            pageIndex,
            publicToken,
            embedTokenExpiresAtUtc,
            settings.Theme,
            settings.Mode,
            target.ProductUrlTemplate,
            target.SearchUrlTemplate,
            target.ExistingCartUrl,
            target.ExistingCartMethod,
            target.AccessExpiresAt,
            $"/view/{target.CatalogId}/viewer/{pageIndex}?token={Uri.EscapeDataString(publicToken)}&embed=1&embedTarget=1&embedKey={Uri.EscapeDataString(target.EmbedKey)}&commerceMode={Uri.EscapeDataString(target.CommerceMode)}&hostActionMode={Uri.EscapeDataString(target.HostActionMode)}");
    }

    private static string GenerateEmbedKey()
    {
        return $"emb_{Guid.NewGuid():N}";
    }

    private static string? NormalizeEmbedTargetType(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? "catalog" : raw.Trim().ToLowerInvariant();
        return value is "catalog" or "catalog_page" ? value : null;
    }

    private static string? NormalizeCommerceMode(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? "catalog_only" : raw.Trim().ToLowerInvariant();
        return value is "catalog_only" or "host_cart" or "host_availability_cart" ? value : null;
    }

    private static string? NormalizeHostActionMode(string? raw, string commerceMode)
    {
        if (string.Equals(commerceMode, "catalog_only", StringComparison.Ordinal))
        {
            return "none";
        }

        var value = string.IsNullOrWhiteSpace(raw) ? "search_redirect" : raw.Trim().ToLowerInvariant();
        return value is "none" or "product_redirect" or "search_redirect" or "existing_cart_api" or "existing_cart_js" or "custom"
            ? value
            : null;
    }

    private static string NormalizeCartMethod(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? "POST" : raw.Trim().ToUpperInvariant();
        return value is "GET" or "POST" ? value : "POST";
    }

    private static string? ValidateHostActionSettings(
        string commerceMode,
        string hostActionMode,
        string? productUrlTemplate,
        string? searchUrlTemplate,
        string? existingCartUrl)
    {
        if (string.Equals(commerceMode, "catalog_only", StringComparison.Ordinal))
        {
            return null;
        }

        return hostActionMode switch
        {
            "product_redirect" when string.IsNullOrWhiteSpace(productUrlTemplate) =>
                "Urun sayfasina yonlendirme icin bir urun URL sablonu girin.",
            "search_redirect" when string.IsNullOrWhiteSpace(searchUrlTemplate) =>
                "Site ici arama icin bir arama URL sablonu girin.",
            "existing_cart_api" when string.IsNullOrWhiteSpace(existingCartUrl) =>
                "Mevcut sepet API modu icin cart URL girin.",
            _ => null
        };
    }

    private static string? NormalizeOptionalText(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        return value.Length <= maxLength ? value : value[..maxLength].Trim();
    }

    public sealed class VerifyOriginRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string? StoreSlug { get; set; }
        public string? EmbedKey { get; set; }
        public string? Origin { get; set; }
    }

    public sealed class ResolveEmbedTargetRequest
    {
        public string EmbedKey { get; set; } = string.Empty;
        public string? Origin { get; set; }
    }

    public sealed class UpdateEmbedSettingsRequest
    {
        public string[]? AllowedOrigins { get; set; }
        public string? Theme { get; set; }
        public string? Mode { get; set; }
        public string? StoreSlug { get; set; }
    }

    public sealed class EmbedEventIngestRequest
    {
        public string? PublicToken { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? PageUrl { get; set; }
        public JsonElement Payload { get; set; }
    }

    public sealed class CreateDomainChallengeRequest
    {
        public string Origin { get; set; } = string.Empty;
        public string Method { get; set; } = "dns_txt";
    }

    public sealed class CreateEmbedTargetRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "catalog";
        public Guid CatalogId { get; set; }
        public Guid? CatalogPageId { get; set; }
        public string CommerceMode { get; set; } = "catalog_only";
        public string? HostActionMode { get; set; }
        public string? ProductUrlTemplate { get; set; }
        public string? SearchUrlTemplate { get; set; }
        public string? ExistingCartUrl { get; set; }
        public string? ExistingCartMethod { get; set; }
        public DateTime? AccessExpiresAt { get; set; }
        public bool? IsActive { get; set; }
    }

    private sealed record EmbedResolvedConfig(
        Guid OwnerUserId,
        bool WhiteLabel,
        string EmbedKey,
        string TargetType,
        string CommerceMode,
        string HostActionMode,
        Guid CatalogId,
        Guid? CatalogPageId,
        int? PageNumber,
        int PageIndex,
        string PublicToken,
        DateTime EmbedTokenExpiresAtUtc,
        string Theme,
        string Mode,
        string? ProductUrlTemplate,
        string? SearchUrlTemplate,
        string? ExistingCartUrl,
        string? ExistingCartMethod,
        DateTime? AccessExpiresAtUtc,
        string RuntimePath);

    private sealed class EmbedTargetPrepareResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public EmbedTarget? Value { get; }

        private EmbedTargetPrepareResult(bool isSuccess, EmbedTarget? value, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
        }

        public static EmbedTargetPrepareResult Ok(EmbedTarget value) => new(true, value, null);
        public static EmbedTargetPrepareResult Fail(string errorMessage) => new(false, null, errorMessage);
    }

    private async Task<ResolvedEmbedTarget?> ResolveEmbedTargetAsync(string? publicToken, string? storeSlug, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(publicToken))
        {
            var token = publicToken.Trim();
            var payload = _publicAccessTokenService.Validate(token);
            if (payload == null || payload.UserId == Guid.Empty)
            {
                return null;
            }

            var owner = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == payload.UserId)
                .Select(u => new { u.Id, u.SubscriptionPlan, u.PublicStoreSlug, u.PlanExpiresAt, u.Role, u.PublicLinkEnabled })
                .SingleOrDefaultAsync(cancellationToken);

            if (owner == null || !owner.PublicLinkEnabled)
            {
                return null;
            }

            if (string.Equals(owner.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (owner.PlanExpiresAt.HasValue && owner.PlanExpiresAt.Value <= DateTime.UtcNow)
            {
                return null;
            }

            return new ResolvedEmbedTarget(owner.Id, token, owner.PublicStoreSlug, owner.SubscriptionPlan);
        }

        var normalizedSlug = NormalizeStoreSlug(storeSlug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return null;
        }

        var user = await _dbContext.Users
            .Where(u => u.PublicStoreSlug == normalizedSlug)
            .Select(u => new { u.Id, u.PublicStoreSlug, u.SubscriptionPlan, u.PublicLinkEnabled, u.PublicLinkVersion, u.PlanExpiresAt, u.Role })
            .SingleOrDefaultAsync(cancellationToken);

        if (user == null || !user.PublicLinkEnabled)
        {
            return null;
        }

        if (string.Equals(user.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (user.PlanExpiresAt.HasValue && user.PlanExpiresAt.Value <= DateTime.UtcNow)
        {
            return null;
        }

        var generatedToken = _publicCatalogLinkService.GetOrCreateToken(user.Id, user.PublicLinkVersion);
        return new ResolvedEmbedTarget(user.Id, generatedToken, user.PublicStoreSlug, user.SubscriptionPlan);
    }

    private async Task<string> EnsureUserStoreSlugAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Kullanıcı bulunamadı.");
        }

        if (!string.IsNullOrWhiteSpace(user.PublicStoreSlug))
        {
            return user.PublicStoreSlug;
        }

        user.PublicStoreSlug = await BuildUniqueStoreSlugAsync(user.CompanyName, $"{user.FirstName} {user.LastName}", user.Id, cancellationToken);
        user.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.PublicStoreSlug;
    }

    private async Task<string> UpsertStoreSlugAsync(Guid userId, string? requestedStoreSlug, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Kullanıcı bulunamadı.");
        }

        var normalizedRequested = NormalizeStoreSlug(requestedStoreSlug);
        if (string.IsNullOrWhiteSpace(normalizedRequested))
        {
            normalizedRequested = await BuildUniqueStoreSlugAsync(user.CompanyName, $"{user.FirstName} {user.LastName}", user.Id, cancellationToken);
        }
        else
        {
            var ownerExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != userId && u.PublicStoreSlug == normalizedRequested, cancellationToken);

            if (ownerExists)
            {
                throw new InvalidOperationException("Bu mağaza kodu zaten kullanımda. Başka bir slug seçin.");
            }
        }

        user.PublicStoreSlug = normalizedRequested;
        user.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return normalizedRequested;
    }

    private async Task<string> BuildUniqueStoreSlugAsync(string? companyName, string? fallbackName, Guid userId, CancellationToken cancellationToken)
    {
        var baseSlug = NormalizeStoreSlug(companyName);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = NormalizeStoreSlug(fallbackName);
        }

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = $"magaza-{userId.ToString("N")[..6]}";
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id != userId && u.PublicStoreSlug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeStoreSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var slug = Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');
        if (slug.Length > 96)
        {
            slug = slug[..96].Trim('-');
        }

        return slug;
    }

    private string ResolveAppBaseUrl()
    {
        var configured = _configuration["Frontend:BaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || HttpContext.Request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return "http://localhost:4200";
        }

        return $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
    }

    private int GetEmbedSessionExpirationMinutes()
    {
        var configured = _configuration.GetValue<int?>("EmbedAccessToken:ExpirationMinutes");
        if (configured.HasValue && configured.Value > 0)
        {
            return configured.Value;
        }

        return 15;
    }

    private sealed record ResolvedEmbedTarget(Guid UserId, string PublicToken, string? StoreSlug, SubscriptionPlan Plan);
}
