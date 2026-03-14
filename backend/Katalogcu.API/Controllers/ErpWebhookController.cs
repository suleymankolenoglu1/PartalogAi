using Katalogcu.API.Services;
using Katalogcu.Application.Features.Products.Commands.ApplyErpInventoryWebhook;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/erp/webhooks")]
public sealed class ErpWebhookController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IOptions<ErpGatewayOptions> _options;
    private readonly ILogger<ErpWebhookController> _logger;

    public ErpWebhookController(
        ISender sender,
        IOptions<ErpGatewayOptions> options,
        ILogger<ErpWebhookController> logger)
    {
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> PushInventory(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromBody] ErpInventoryWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var client = ErpGatewayService.ResolveWebhookClient(_options.Value, apiKey);
        if (client == null)
        {
            return Unauthorized(new { message = "Geçersiz ERP webhook API key." });
        }

        var command = new ApplyErpInventoryWebhookCommand(
            client.OwnerUserId,
            string.IsNullOrWhiteSpace(client.Provider) ? _options.Value.DefaultProvider : client.Provider,
            string.IsNullOrWhiteSpace(client.Name) ? "erp-webhook" : client.Name,
            request.EventId,
            request.OccurredAtUtc,
            (request.Items ?? []).Select(x => new ApplyErpInventoryWebhookItemInput
            {
                ProductId = x.ProductId,
                PartCode = x.PartCode,
                ProductName = x.ProductName,
                ExternalProductId = x.ExternalProductId,
                UnitPrice = x.UnitPrice,
                StockQuantity = x.StockQuantity,
                Currency = x.Currency
            }).ToList());

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "ERP webhook işlenemedi. Client={ClientName} Error={ErrorCode} Message={Message}",
                client.Name,
                result.ErrorCode,
                result.ErrorMessage);

            return result.ErrorCode switch
            {
                "validation" => BadRequest(result.ErrorMessage),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage)
            };
        }

        return Ok(new
        {
            message = "ERP inventory webhook işlendi.",
            processedCount = result.Value?.ProcessedCount ?? 0,
            updatedProductCount = result.Value?.UpdatedProductCount ?? 0,
            skippedCount = result.Value?.SkippedCount ?? 0
        });
    }
}

public sealed class ErpInventoryWebhookRequest
{
    public string? EventId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public List<ErpInventoryWebhookItemDto> Items { get; set; } = [];
}

public sealed class ErpInventoryWebhookItemDto
{
    public Guid? ProductId { get; set; }
    public string? PartCode { get; set; }
    public string? ProductName { get; set; }
    public string? ExternalProductId { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? StockQuantity { get; set; }
    public string? Currency { get; set; }
}
