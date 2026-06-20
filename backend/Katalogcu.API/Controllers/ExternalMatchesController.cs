using FluentValidation;
using Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalMatch;
using Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalProductByUrl;
using Katalogcu.Application.Features.ExternalMatches.Commands.BulkApproveCatalogExternalMatches;
using Katalogcu.Application.Features.ExternalMatches.Commands.RejectCatalogExternalMatch;
using Katalogcu.Application.Features.ExternalMatches.Commands.StartCatalogExternalMatching;
using Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogAutoMatchedExternalMatches;
using Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogApprovedExternalMatches;
using Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogExternalMatchQueue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PrivilegedUser")]
[Route("api/catalogs/{catalogId:guid}/external-matches")]
[ApiController]
public sealed class ExternalMatchesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<ExternalMatchesController> _logger;

    public ExternalMatchesController(ISender sender, ILogger<ExternalMatchesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(Guid catalogId)
    {
        try
        {
            var result = await _sender.Send(new GetCatalogExternalMatchQueueQuery(catalogId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Eşleşme kuyruğu alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("auto-matched")]
    public async Task<IActionResult> GetAutoMatched(Guid catalogId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sender.Send(new GetCatalogAutoMatchedExternalMatchesQuery(catalogId, page, pageSize));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Auto matched listesi alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("start")]
    [EnableRateLimiting("external-match-start")]
    public async Task<IActionResult> StartMatching(Guid catalogId, [FromBody] StartMatchingRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Catalog external matching started. CatalogId={CatalogId} ExternalSiteId={ExternalSiteId} UserId={UserId}",
                catalogId,
                request.ExternalSiteId,
                GetCurrentUserId());

            var result = await _sender.Send(new StartCatalogExternalMatchingCommand(catalogId, request.ExternalSiteId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Eşleşme başlatılamadı.")
                };
            }

            _logger.LogInformation(
                "Catalog external matching completed. CatalogId={CatalogId} ExternalSiteId={ExternalSiteId} UserId={UserId} CandidateCount={CandidateCount}",
                result.Value!.CatalogId,
                result.Value.ExternalSiteId,
                GetCurrentUserId(),
                result.Value.CandidateCount);

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private string GetCurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(Guid catalogId)
    {
        try
        {
            var result = await _sender.Send(new GetCatalogApprovedExternalMatchesQuery(catalogId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Onaylı eşleşmeler alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{matchId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid catalogId, Guid matchId, [FromBody] ReviewNoteRequest? request)
    {
        try
        {
            var result = await _sender.Send(new ApproveCatalogExternalMatchCommand(matchId, request?.ReviewNote));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Eşleşme onaylanamadı.")
                };
            }

            if (result.Value?.CatalogItemId == Guid.Empty)
            {
                return StatusCode(500, "Eşleşme sonucu alınamadı.");
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{matchId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid catalogId, Guid matchId, [FromBody] ReviewNoteRequest? request)
    {
        try
        {
            var result = await _sender.Send(new RejectCatalogExternalMatchCommand(matchId, request?.ReviewNote));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Eşleşme reddedilemedi.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("approve-by-url")]
    public async Task<IActionResult> ApproveByUrl(Guid catalogId, [FromBody] ApproveByUrlRequest request)
    {
        try
        {
            var result = await _sender.Send(new ApproveCatalogExternalProductByUrlCommand(
                request.CatalogItemId,
                request.ExternalSiteId,
                request.ProductUrl,
                request.ProductTitle,
                request.ReviewNote));

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "URL ile onay işlemi tamamlanamadı.")
                };
            }

            if (result.Value is not null && request.CatalogIdCheckEnabled && request.ExpectedCatalogItemCatalogId != Guid.Empty && request.ExpectedCatalogItemCatalogId != catalogId)
            {
                return BadRequest("Katalog item route içindeki katalog ile uyumlu değil.");
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove(Guid catalogId, [FromBody] BulkApproveRequest request)
    {
        try
        {
            var result = await _sender.Send(new BulkApproveCatalogExternalMatchesCommand(request.MatchIds, request.ReviewNote));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Toplu onay işlemi tamamlanamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public sealed class ReviewNoteRequest
{
    public string? ReviewNote { get; set; }
}

public sealed class ApproveByUrlRequest
{
    public Guid CatalogItemId { get; set; }
    public Guid ExternalSiteId { get; set; }
    public string ProductUrl { get; set; } = string.Empty;
    public string? ProductTitle { get; set; }
    public string? ReviewNote { get; set; }

    // Route catalogId ile UI tarafında opsiyonel guard için bırakıldı.
    public bool CatalogIdCheckEnabled { get; set; }
    public Guid ExpectedCatalogItemCatalogId { get; set; }
}

public sealed class BulkApproveRequest
{
    public IReadOnlyCollection<Guid> MatchIds { get; set; } = [];
    public string? ReviewNote { get; set; }
}

public sealed class StartMatchingRequest
{
    public Guid ExternalSiteId { get; set; }
}
