using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogAutoMatchedExternalMatches;

public sealed record GetCatalogAutoMatchedExternalMatchesQuery(Guid CatalogId, int Page = 1, int PageSize = 50)
    : IRequest<OperationResult<GetCatalogAutoMatchedExternalMatchesResponse>>;

public sealed class GetCatalogAutoMatchedExternalMatchesItemDto
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
    public DateTime? MatchedAtUtc { get; init; }
    public string? MatchReasonsJson { get; init; }
}

public sealed class GetCatalogAutoMatchedExternalMatchesResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<GetCatalogAutoMatchedExternalMatchesItemDto> Items { get; init; } = [];
}
