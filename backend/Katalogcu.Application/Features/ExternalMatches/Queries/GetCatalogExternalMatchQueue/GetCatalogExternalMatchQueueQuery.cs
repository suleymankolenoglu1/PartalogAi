using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogExternalMatchQueue;

public sealed record GetCatalogExternalMatchQueueQuery(Guid CatalogId)
    : IRequest<OperationResult<GetCatalogExternalMatchQueueResponse>>;

public sealed class GetCatalogExternalMatchQueueItemDto
{
    public Guid MatchId { get; init; }
    public Guid CatalogItemId { get; init; }
    public Guid ExternalSiteId { get; init; }
    public Guid? ExternalProductId { get; init; }
    public string? ExternalProductUrl { get; init; }
    public string? ExternalProductTitle { get; init; }
    public decimal ConfidenceScore { get; init; }
    public string Status { get; init; } = string.Empty;
    public string MatchedBy { get; init; } = string.Empty;
    public string? MatchReasonsJson { get; init; }
}

public sealed class GetCatalogExternalMatchQueueResponse
{
    public IReadOnlyList<GetCatalogExternalMatchQueueItemDto> Items { get; init; } = [];
}
