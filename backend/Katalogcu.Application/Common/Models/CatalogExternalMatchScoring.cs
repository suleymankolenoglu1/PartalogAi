namespace Katalogcu.Application.Common.Models;

public static class CatalogExternalMatchScoring
{
    public const decimal PartCodeExactWeight = 0.70m;
    public const decimal OemOverlapWeight = 0.25m;
    public const decimal TitleSimilarityWeight = 0.20m;
    public const decimal BrandSimilarityWeight = 0.10m;
    public const decimal CategorySimilarityWeight = 0.05m;

    public const decimal AutoMatchedThreshold = 0.90m;
    public const decimal NeedsReviewThreshold = 0.60m;
}
