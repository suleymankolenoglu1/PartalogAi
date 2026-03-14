using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ApplyErpInventoryWebhook;

public sealed record ApplyErpInventoryWebhookCommand(
    Guid OwnerUserId,
    string Provider,
    string Source,
    string? EventId,
    DateTime? OccurredAtUtc,
    IReadOnlyList<ApplyErpInventoryWebhookItemInput> Items)
    : IRequest<OperationResult<ApplyErpInventoryWebhookResponse>>;

public sealed class ApplyErpInventoryWebhookItemInput
{
    public Guid? ProductId { get; init; }
    public string? PartCode { get; init; }
    public string? ProductName { get; init; }
    public string? ExternalProductId { get; init; }
    public decimal? UnitPrice { get; init; }
    public int? StockQuantity { get; init; }
    public string? Currency { get; init; }
}

public sealed class ApplyErpInventoryWebhookResponse
{
    public int ProcessedCount { get; init; }
    public int UpdatedProductCount { get; init; }
    public int SkippedCount { get; init; }
}
