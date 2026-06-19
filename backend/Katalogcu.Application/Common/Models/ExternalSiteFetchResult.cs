namespace Katalogcu.Application.Common.Models;

public sealed class ExternalSiteFetchResult
{
    public bool Succeeded { get; init; }
    public string? ErrorSummary { get; init; }
    public int ProductCount { get; init; }
    public decimal? SkuCoverage { get; init; }
    public decimal? OemCoverage { get; init; }
    public string RawStatsJson { get; init; } = "{}";
    public IReadOnlyList<CrawledProduct> Products { get; init; } = [];
}

public sealed class CrawledProduct
{
    public string SourceUrl { get; init; } = string.Empty;
    public string? CanonicalUrl { get; init; }
    public string? Title { get; init; }
    public string? Sku { get; init; }
    public string? PartCode { get; init; }
    public string? Brand { get; init; }
    public IReadOnlyList<string> CategoryPath { get; init; } = [];
    public string? ImageUrl { get; init; }
    public string? AvailabilityText { get; init; }
    public string? PriceText { get; init; }
    public string? Currency { get; init; }
    public IReadOnlyList<string> OemNumbers { get; init; } = [];
    public string? RawPayloadJson { get; init; }
}

public sealed class NormalizedExternalProductOemRecord
{
    public string NormalizedValue { get; init; } = string.Empty;
    public string OriginalValue { get; init; } = string.Empty;
}

public sealed class NormalizedExternalProductRecord
{
    public Katalogcu.Domain.Entities.ExternalProduct Product { get; init; } = null!;
    public IReadOnlyList<NormalizedExternalProductOemRecord> OemNumbers { get; init; } = [];
}
