using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ExternalSite : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string PreferredCrawlMode { get; set; } = "auto";
    public DateTime? LastCrawlAtUtc { get; set; }
    public DateTime? LastSuccessfulCrawlAtUtc { get; set; }

    public AppUser? User { get; set; }
    public ICollection<ExternalSiteCrawl> Crawls { get; set; } = [];
    public ICollection<ExternalProduct> Products { get; set; } = [];
}
