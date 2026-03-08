using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.API.Services;
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
        return eventName is "part:viewed" or "cart:add" or "checkout:start";
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

    public sealed class VerifyOriginRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string? StoreSlug { get; set; }
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
                .Select(u => new { u.Id, u.SubscriptionPlan, u.PublicStoreSlug })
                .SingleOrDefaultAsync(cancellationToken);

            if (owner == null)
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
            .Select(u => new { u.Id, u.PublicStoreSlug, u.SubscriptionPlan, u.PublicLinkEnabled, u.PublicLinkVersion })
            .SingleOrDefaultAsync(cancellationToken);

        if (user == null || !user.PublicLinkEnabled)
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

    private sealed record ResolvedEmbedTarget(Guid UserId, string PublicToken, string? StoreSlug, SubscriptionPlan Plan);
}
