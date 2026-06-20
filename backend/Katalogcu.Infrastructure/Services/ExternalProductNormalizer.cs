using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalProductNormalizer : IExternalProductNormalizer
{
    public IReadOnlyList<NormalizedExternalProductRecord> Normalize(
        Guid externalSiteId,
        Guid crawlId,
        IReadOnlyList<CrawledProduct> products)
    {
        return products
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceUrl))
            .Select(x =>
            {
                var normalizedOems = x.OemNumbers
                    .Select(original => new NormalizedExternalProductOemRecord
                    {
                        OriginalValue = original.Trim(),
                        NormalizedValue = NormalizeOem(original)
                    })
                    .Where(oem => !string.IsNullOrWhiteSpace(oem.NormalizedValue))
                    .DistinctBy(oem => oem.NormalizedValue, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new NormalizedExternalProductRecord
                {
                    Product = new ExternalProduct
                    {
                        ExternalSiteId = externalSiteId,
                        LastSeenInCrawlId = crawlId,
                        SourceUrl = NormalizeUrl(x.SourceUrl),
                        CanonicalUrl = NormalizeOptionalUrl(x.CanonicalUrl),
                        Title = TrimOrNull(x.Title, 512),
                        Sku = TrimOrNull(x.Sku, 128),
                        PartCode = TrimOrNull(x.PartCode, 128),
                        Brand = TrimOrNull(x.Brand, 160),
                        CategoryPathJson = x.CategoryPath.Count == 0 ? null : JsonSerializer.Serialize(x.CategoryPath),
                        ImageUrl = NormalizeOptionalUrl(x.ImageUrl),
                        AvailabilityText = TrimOrNull(x.AvailabilityText),
                        PriceText = TrimOrNull(x.PriceText),
                        Currency = TrimOrNull(x.Currency, 8),
                        RawPayloadJson = string.IsNullOrWhiteSpace(x.RawPayloadJson) ? null : x.RawPayloadJson.Trim(),
                        IsActive = true,
                        LastSeenAtUtc = DateTime.UtcNow
                    },
                    OemNumbers = normalizedOems
                };
            })
            .ToList();
    }

    private static string NormalizeOem(string value)
    {
        var trimmed = value.Trim().ToUpperInvariant();
        return Regex.Replace(trimmed, "[^A-Z0-9]", string.Empty);
    }

    private static string NormalizeUrl(string value)
    {
        var uri = new Uri(value.Trim(), UriKind.Absolute);
        return uri.ToString().TrimEnd('/');
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeUrl(value);
    }

    private static string? TrimOrNull(string? value, int? max = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (max.HasValue && trimmed.Length > max.Value)
        {
            return trimmed[..max.Value];
        }

        return trimmed;
    }
}
