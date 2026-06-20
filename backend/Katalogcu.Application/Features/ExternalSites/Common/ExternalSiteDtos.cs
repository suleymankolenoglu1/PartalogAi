namespace Katalogcu.Application.Features.ExternalSites.Common;

public sealed class ExternalSiteCrawlSummaryDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ExecutionMode { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public decimal? SkuCoverage { get; init; }
    public decimal? OemCoverage { get; init; }
    public string? ErrorSummary { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class ExternalSiteDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PreferredCrawlMode { get; init; } = string.Empty;
    public DateTime? LastCrawlAtUtc { get; init; }
    public DateTime? LastSuccessfulCrawlAtUtc { get; init; }
    public DateTime CreatedDate { get; init; }
    public ExternalSiteCrawlSummaryDto? LatestCrawl { get; init; }
}

public sealed class ExternalProductListItemDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
    public string? CanonicalUrl { get; init; }
    public string? Sku { get; init; }
    public string? PartCode { get; init; }
    public string? Brand { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
    public bool IsActive { get; init; }
    public int OemCount { get; init; }
}

public sealed class ExternalProductsBySiteResponse
{
    public Guid SiteId { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ExternalProductListItemDto> Items { get; init; } = [];
    public ExternalSiteCrawlSummaryDto? LatestCrawl { get; init; }
}

public sealed class ManualImportResultDto
{
    public Guid ManualImportFileId { get; init; }
    public Guid SiteId { get; init; }
    public int RowCount { get; init; }
    public int ImportedProductCount { get; init; }
    public int FailedRowCount { get; init; }
    public string FileType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ErrorSummary { get; init; }
}

public sealed class ManualImportHistoryItemDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ErrorSummary { get; init; }
    public DateTime ImportedAtUtc { get; init; }
}

public sealed class ManualImportHistoryResponse
{
    public Guid SiteId { get; init; }
    public IReadOnlyList<ManualImportHistoryItemDto> Items { get; init; } = [];
}
