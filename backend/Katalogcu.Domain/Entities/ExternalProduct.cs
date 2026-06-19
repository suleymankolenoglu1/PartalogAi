using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ExternalProduct : BaseEntity
{
    public Guid ExternalSiteId { get; set; }
    public Guid? LastSeenInCrawlId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string? Title { get; set; }
    public string? Sku { get; set; }
    public string? PartCode { get; set; }
    public string? Brand { get; set; }
    public string? CategoryPathJson { get; set; }
    public string? ImageUrl { get; set; }
    public string? AvailabilityText { get; set; }
    public string? PriceText { get; set; }
    public string? Currency { get; set; }
    public string? RawPayloadJson { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAtUtc { get; set; }

    public ExternalSite? ExternalSite { get; set; }
    public ExternalSiteCrawl? LastSeenInCrawl { get; set; }
    public ICollection<ExternalProductOemNumber> OemNumbers { get; set; } = [];
    public ICollection<CatalogItemExternalMatch> CatalogMatches { get; set; } = [];
    public ICollection<ExternalProductLinkCheck> LinkChecks { get; set; } = [];
}
