using System.Security.Claims;
using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/embed")]
public class EmbedController : ControllerBase
{
    private readonly IEmbedOriginService _embedOriginService;
    private readonly IPublicAccessTokenService _publicAccessTokenService;
    private readonly IEmbedAnalyticsService _embedAnalyticsService;
    private readonly IEmbedDomainVerificationService _embedDomainVerificationService;

    public EmbedController(
        IEmbedOriginService embedOriginService,
        IPublicAccessTokenService publicAccessTokenService,
        IEmbedAnalyticsService embedAnalyticsService,
        IEmbedDomainVerificationService embedDomainVerificationService)
    {
        _embedOriginService = embedOriginService;
        _publicAccessTokenService = publicAccessTokenService;
        _embedAnalyticsService = embedAnalyticsService;
        _embedDomainVerificationService = embedDomainVerificationService;
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var settings = await _embedOriginService.GetOrCreateAsync(userId, cancellationToken);
        return Ok(new
        {
            userId = settings.UserId,
            allowedOrigins = settings.AllowedOrigins,
            theme = settings.Theme,
            mode = settings.Mode
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
            mode = updated.Mode
        });
    }

    [HttpPost("verify-origin")]
    [AllowAnonymous]
    [EnableRateLimiting("public-embed-events")]
    public async Task<IActionResult> VerifyOrigin([FromBody] VerifyOriginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublicToken))
        {
            return BadRequest(new { allowed = false, reason = "token_required" });
        }

        var payload = _publicAccessTokenService.Validate(request.PublicToken.Trim());
        if (payload == null || payload.UserId == Guid.Empty)
        {
            return BadRequest(new { allowed = false, reason = "invalid_token" });
        }

        var rawOrigin = string.IsNullOrWhiteSpace(request.Origin)
            ? HttpContext.Request.Headers.Origin.ToString()
            : request.Origin;

        var normalizedOrigin = _embedOriginService.NormalizeOrigin(rawOrigin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            return BadRequest(new { allowed = false, reason = "origin_required" });
        }

        var settings = await _embedOriginService.GetOrCreateAsync(payload.UserId, cancellationToken);
        var allowed = settings.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);

        return Ok(new
        {
            allowed,
            reason = allowed ? "ok" : "origin_not_allowed",
            origin = normalizedOrigin,
            ownerUserId = payload.UserId,
            theme = settings.Theme,
            mode = settings.Mode
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
            instructions = row.Method == "dns_txt"
                ? new
                {
                    type = "dns_txt",
                    recordName = $"_partalog-challenge.{row.Domain}",
                    recordType = "TXT",
                    recordValue = row.ChallengeToken
                }
                : new
                {
                    type = "file",
                    filePath = "/.well-known/partalog-verification.txt",
                    fileUrl = $"{row.Origin}/.well-known/partalog-verification.txt",
                    fileContent = row.ChallengeToken
                }
        };
    }

    public sealed class VerifyOriginRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string? Origin { get; set; }
    }

    public sealed class UpdateEmbedSettingsRequest
    {
        public string[]? AllowedOrigins { get; set; }
        public string? Theme { get; set; }
        public string? Mode { get; set; }
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
}
