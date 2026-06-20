using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Infrastructure.Services;

public sealed class CatalogExternalMatchService : ICatalogExternalMatchService
{
    public int CatalogItemBatchSize => 200;
    public int ExternalProductBatchSize => 500;

    public IReadOnlyList<CatalogItemExternalMatch> BuildCandidates(
        Catalog catalog,
        IReadOnlyList<CatalogItem> catalogItems,
        IReadOnlyList<ExternalProduct> externalProducts,
        Guid externalSiteId)
    {
        var nowUtc = DateTime.UtcNow;
        var results = new List<CatalogItemExternalMatch>();

        foreach (var item in catalogItems)
        {
            foreach (var product in externalProducts)
            {
                var score = 0m;
                var reasons = new List<string>();

                if (HasExactPartCodeMatch(item, product))
                {
                    score += CatalogExternalMatchScoring.PartCodeExactWeight;
                    reasons.Add("part_code_exact");
                }

                if (HasOemOverlap(item, product))
                {
                    score += CatalogExternalMatchScoring.OemOverlapWeight;
                    reasons.Add("oem_overlap");
                }

                var titleSimilarity = CalculateStringSimilarity(item.PartName, product.Title);
                if (titleSimilarity > 0)
                {
                    score += titleSimilarity * CatalogExternalMatchScoring.TitleSimilarityWeight;
                    reasons.Add($"title_similarity:{titleSimilarity:0.##}");
                }

                var brandSimilarity = CalculateStringSimilarity(item.MachineBrand, product.Brand);
                if (brandSimilarity > 0)
                {
                    score += brandSimilarity * CatalogExternalMatchScoring.BrandSimilarityWeight;
                    reasons.Add($"brand_similarity:{brandSimilarity:0.##}");
                }

                var categorySimilarity = CalculateCategorySimilarity(item.MachineGroup, product.CategoryPathJson);
                if (categorySimilarity > 0)
                {
                    score += categorySimilarity * CatalogExternalMatchScoring.CategorySimilarityWeight;
                    reasons.Add($"category_similarity:{categorySimilarity:0.##}");
                }

                if (score < CatalogExternalMatchScoring.NeedsReviewThreshold)
                {
                    continue;
                }

                var status = score >= CatalogExternalMatchScoring.AutoMatchedThreshold
                    ? "auto_matched"
                    : "needs_review";

                results.Add(new CatalogItemExternalMatch
                {
                    Id = Guid.NewGuid(),
                    CatalogId = catalog.Id,
                    CatalogPageId = ResolveCatalogPageId(catalog, item.PageNumber),
                    CatalogItemId = item.Id,
                    ExternalSiteId = externalSiteId,
                    ExternalProductId = product.Id,
                    ExternalProductUrl = product.CanonicalUrl ?? product.SourceUrl,
                    ExternalProductTitle = product.Title,
                    ConfidenceScore = decimal.Round(score, 4),
                    Status = status,
                    MatchedBy = "ai",
                    IsActive = status == "auto_matched",
                    MatchedAtUtc = nowUtc,
                    MatchReasonsJson = JsonSerializer.Serialize(reasons),
                    CreatedDate = nowUtc
                });
            }
        }

        return results
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenBy(x => x.ExternalProductTitle)
            .ToList();
    }

    public void ReplaceAiCandidates(
        Guid catalogId,
        IEnumerable<CatalogItemExternalMatch> existingMatches,
        IEnumerable<CatalogItemExternalMatch> newMatches,
        out IReadOnlyList<CatalogItemExternalMatch> matchesToRemove,
        out IReadOnlyList<CatalogItemExternalMatch> matchesToAdd)
    {
        matchesToRemove = existingMatches
            .Where(x =>
                x.CatalogId == catalogId &&
                !string.Equals(x.Status, "approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            .ToList();

        matchesToAdd = newMatches.ToList();
    }

    private static bool HasExactPartCodeMatch(CatalogItem item, ExternalProduct product)
    {
        if (string.IsNullOrWhiteSpace(item.PartCode) || string.IsNullOrWhiteSpace(product.PartCode) && string.IsNullOrWhiteSpace(product.Sku))
        {
            return false;
        }

        var itemCode = NormalizeToken(item.PartCode);
        var productCode = NormalizeToken(product.PartCode ?? product.Sku!);
        return !string.IsNullOrWhiteSpace(itemCode) && itemCode == productCode;
    }

    private static bool HasOemOverlap(CatalogItem item, ExternalProduct product)
    {
        var itemCandidates = new[]
        {
            NormalizeToken(item.PartCode),
            NormalizeToken(item.RefNumber)
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (itemCandidates.Count == 0)
        {
            return false;
        }

        return product.OemNumbers.Any(x => itemCandidates.Contains(x.NormalizedOemNumber));
    }

    private static decimal CalculateStringSimilarity(string? left, string? right)
    {
        var a = Tokenize(left);
        var b = Tokenize(right);
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (decimal)intersection / union;
    }

    private static decimal CalculateCategorySimilarity(string? machineGroup, string? categoryPathJson)
    {
        if (string.IsNullOrWhiteSpace(machineGroup) || string.IsNullOrWhiteSpace(categoryPathJson))
        {
            return 0;
        }

        try
        {
            var categories = JsonSerializer.Deserialize<List<string>>(categoryPathJson) ?? [];
            return CalculateStringSimilarity(machineGroup, string.Join(' ', categories));
        }
        catch
        {
            return 0;
        }
    }

    private static Guid? ResolveCatalogPageId(Catalog catalog, string? pageNumber)
    {
        if (string.IsNullOrWhiteSpace(pageNumber))
        {
            return null;
        }

        return catalog.Pages.FirstOrDefault(x => x.PageNumber.ToString() == pageNumber)?.Id;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([' ', '-', '_', '/', '\\', ',', '.', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
