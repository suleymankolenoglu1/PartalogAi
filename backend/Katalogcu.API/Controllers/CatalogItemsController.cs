using Katalogcu.Application.Features.CatalogItems.Commands.CreateCatalogItem;
using Katalogcu.Application.Features.CatalogItems.Commands.DeleteCatalogItem;
using Katalogcu.Application.Features.CatalogItems.Commands.UpdateCatalogItem;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PrivilegedUser")]
[ApiController]
[Route("api/catalog-items")]
public sealed class CatalogItemsController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogItemsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCatalogItemRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new CreateCatalogItemCommand(
            request.CatalogId,
            request.PageNumber,
            userId,
            request.RefNo,
            request.PartCode,
            request.PartName,
            request.Description));

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "not_found" => NotFound(result.ErrorMessage),
                "validation" => BadRequest(result.ErrorMessage),
                _ => StatusCode(500, result.ErrorMessage ?? "Parça satırı oluşturulamadı.")
            };
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCatalogItemRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new UpdateCatalogItemCommand(
            id,
            userId,
            request.RefNo,
            request.PartCode,
            request.PartName,
            request.Description));

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "not_found" => NotFound(result.ErrorMessage),
                "validation" => BadRequest(result.ErrorMessage),
                _ => StatusCode(500, result.ErrorMessage ?? "Parça satırı güncellenemedi.")
            };
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new DeleteCatalogItemCommand(id, userId));
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "not_found" => NotFound(result.ErrorMessage),
                "validation" => BadRequest(result.ErrorMessage),
                _ => StatusCode(500, result.ErrorMessage ?? "Parça satırı silinemedi.")
            };
        }

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idString, out var id) ? id : Guid.Empty;
    }

    public sealed class CreateCatalogItemRequest
    {
        public Guid CatalogId { get; set; }
        public int PageNumber { get; set; }
        public string RefNo { get; set; } = string.Empty;
        public string PartCode { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public sealed class UpdateCatalogItemRequest
    {
        public string RefNo { get; set; } = string.Empty;
        public string PartCode { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
