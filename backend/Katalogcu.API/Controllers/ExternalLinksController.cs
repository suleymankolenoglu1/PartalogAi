using FluentValidation;
using Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinkByCatalogItem;
using Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinksByCatalog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PrivilegedUser")]
[ApiController]
public sealed class ExternalLinksController : ControllerBase
{
    private readonly ISender _sender;

    public ExternalLinksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("api/catalogs/{catalogId:guid}/published-links")]
    public async Task<IActionResult> GetPublishedLinks(Guid catalogId)
    {
        try
        {
            var result = await _sender.Send(new GetPublishedExternalLinksByCatalogQuery(catalogId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Yayınlanmış linkler alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("api/catalog-items/{itemId:guid}/published-link")]
    public async Task<IActionResult> GetPublishedLinkByCatalogItem(Guid itemId)
    {
        try
        {
            var result = await _sender.Send(new GetPublishedExternalLinkByCatalogItemQuery(itemId));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Yayınlanmış link alınamadı.")
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
