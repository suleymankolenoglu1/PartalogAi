using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class CatalogItemExternalMatch : BaseEntity
{
    public Guid CatalogId { get; set; }
    public Guid? CatalogPageId { get; set; }
    public Guid CatalogItemId { get; set; }
    public Guid ExternalSiteId { get; set; }
    public Guid? ExternalProductId { get; set; }
    public string? ExternalProductUrl { get; set; }
    public string? ExternalProductTitle { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Status { get; set; } = "needs_review";
    public string MatchedBy { get; set; } = "ai";
    public bool IsActive { get; set; }
    public DateTime? MatchedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public string? MatchReasonsJson { get; set; }
    public DateTime? LastLinkCheckAtUtc { get; set; }
    public int? LastLinkStatusCode { get; set; }
    public bool? IsLinkHealthy { get; set; }

    public Catalog? Catalog { get; set; }
    public CatalogPage? CatalogPage { get; set; }
    public CatalogItem? CatalogItem { get; set; }
    public ExternalSite? ExternalSite { get; set; }
    public ExternalProduct? ExternalProduct { get; set; }
    public AppUser? ReviewedByUser { get; set; }
}
