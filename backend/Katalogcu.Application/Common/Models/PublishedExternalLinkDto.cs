namespace Katalogcu.Application.Common.Models;

public sealed class PublishedExternalLinkDto
{
    public Guid MatchId { get; init; }
    public Guid CatalogId { get; init; }
    public Guid CatalogItemId { get; init; }
    public Guid ExternalSiteId { get; init; }
    public Guid? ExternalProductId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool? IsLinkHealthy { get; init; }
    public DateTime? LastLinkCheckAtUtc { get; init; }
    public int? LastLinkStatusCode { get; init; }
}
