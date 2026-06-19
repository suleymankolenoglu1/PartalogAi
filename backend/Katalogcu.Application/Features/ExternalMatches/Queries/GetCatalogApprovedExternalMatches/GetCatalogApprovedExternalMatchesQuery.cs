using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogApprovedExternalMatches;

public sealed record GetCatalogApprovedExternalMatchesQuery(Guid CatalogId)
    : IRequest<OperationResult<GetCatalogApprovedExternalMatchesResponse>>;

public sealed class GetCatalogApprovedExternalMatchesItemDto
{
    public Guid MatchId { get; init; }
    public Guid CatalogItemId { get; init; }
    public Guid ExternalSiteId { get; init; }
    public Guid? ExternalProductId { get; init; }
    public string? ExternalProductUrl { get; init; }
    public string? ExternalProductTitle { get; init; }
    public decimal ConfidenceScore { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool? IsLinkHealthy { get; init; }
    public DateTime? LastLinkCheckAtUtc { get; init; }
    public int? LastLinkStatusCode { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
}

public sealed class GetCatalogApprovedExternalMatchesResponse
{
    public IReadOnlyList<GetCatalogApprovedExternalMatchesItemDto> Items { get; init; } = [];
}
