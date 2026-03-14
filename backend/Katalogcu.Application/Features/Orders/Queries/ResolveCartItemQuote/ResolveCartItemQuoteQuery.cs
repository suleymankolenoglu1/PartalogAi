using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.ResolveCartItemQuote;

public sealed record ResolveCartItemQuoteQuery(
    Guid? AuthenticatedUserId,
    Guid? PublicUserId,
    IReadOnlyCollection<Guid>? PublicCatalogIds,
    Guid ProductId,
    string? PartCode,
    int Quantity)
    : IRequest<OperationResult<ResolveCartItemQuoteResponse>>;

public sealed class ResolveCartItemQuoteResponse
{
    public Guid? ProductId { get; init; }
    public string PartCode { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public int? AvailableStock { get; init; }
    public bool IsAvailable { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Currency { get; init; } = "TRY";
    public DateTime? SynchronizedAtUtc { get; init; }
}
