using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ExternalSiteCrawl : BaseEntity
{
    public Guid ExternalSiteId { get; set; }
    public string TriggerType { get; set; } = "manual";
    public string ExecutionMode { get; set; } = "fetch";
    public string Status { get; set; } = "queued";
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ProductCount { get; set; }
    public decimal? SkuCoverage { get; set; }
    public decimal? OemCoverage { get; set; }
    public string? ErrorSummary { get; set; }
    public string? RawStatsJson { get; set; }

    public ExternalSite? ExternalSite { get; set; }
}
