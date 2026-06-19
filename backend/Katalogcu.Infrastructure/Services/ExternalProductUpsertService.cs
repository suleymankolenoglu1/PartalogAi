using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalProductUpsertService : IExternalProductUpsertService
{
    private readonly AppDbContext _context;

    public ExternalProductUpsertService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> UpsertAsync(
        Guid externalSiteId,
        Guid crawlId,
        IReadOnlyList<NormalizedExternalProductRecord> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return 0;
        }

        var sourceUrls = products
            .Select(x => x.Product.SourceUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingProducts = await _context.ExternalProducts
            .Include(x => x.OemNumbers)
            .Where(x => x.ExternalSiteId == externalSiteId && sourceUrls.Contains(x.SourceUrl))
            .ToListAsync(cancellationToken);

        var existingBySourceUrl = existingProducts.ToDictionary(x => x.SourceUrl, StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;
        var affected = 0;

        foreach (var normalized in products)
        {
            if (existingBySourceUrl.TryGetValue(normalized.Product.SourceUrl, out var existing))
            {
                existing.CanonicalUrl = normalized.Product.CanonicalUrl;
                existing.Title = normalized.Product.Title;
                existing.Sku = normalized.Product.Sku;
                existing.PartCode = normalized.Product.PartCode;
                existing.Brand = normalized.Product.Brand;
                existing.CategoryPathJson = normalized.Product.CategoryPathJson;
                existing.ImageUrl = normalized.Product.ImageUrl;
                existing.AvailabilityText = normalized.Product.AvailabilityText;
                existing.PriceText = normalized.Product.PriceText;
                existing.Currency = normalized.Product.Currency;
                existing.RawPayloadJson = normalized.Product.RawPayloadJson;
                existing.IsActive = true;
                existing.LastSeenAtUtc = nowUtc;
                existing.LastSeenInCrawlId = crawlId;
                existing.UpdatedDate = nowUtc;

                SyncOemNumbers(existing, normalized.OemNumbers, nowUtc);
                affected++;
                continue;
            }

            var product = normalized.Product;
            product.Id = Guid.NewGuid();
            product.CreatedDate = nowUtc;
            product.UpdatedDate = null;
            product.LastSeenAtUtc = nowUtc;
            product.LastSeenInCrawlId = crawlId;
            product.IsActive = true;

            foreach (var oem in normalized.OemNumbers)
            {
                product.OemNumbers.Add(new ExternalProductOemNumber
                {
                    Id = Guid.NewGuid(),
                    ExternalProductId = product.Id,
                    OriginalOemNumber = oem.OriginalValue,
                    NormalizedOemNumber = oem.NormalizedValue,
                    CreatedDate = nowUtc
                });
            }

            await _context.ExternalProducts.AddAsync(product, cancellationToken);
            affected++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return affected;
    }

    public async Task<int> MarkMissingInactiveAsync(
        Guid externalSiteId,
        IReadOnlyCollection<string> seenSourceUrls,
        CancellationToken cancellationToken)
    {
        if (seenSourceUrls.Count == 0)
        {
            return 0;
        }

        var normalizedSeen = seenSourceUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingProducts = await _context.ExternalProducts
            .Where(x => x.ExternalSiteId == externalSiteId && x.IsActive && !normalizedSeen.Contains(x.SourceUrl))
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        foreach (var product in missingProducts)
        {
            product.IsActive = false;
            product.UpdatedDate = nowUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return missingProducts.Count;
    }

    private void SyncOemNumbers(ExternalProduct product, IReadOnlyList<NormalizedExternalProductOemRecord> incomingOems, DateTime nowUtc)
    {
        var incomingByNormalized = incomingOems.ToDictionary(x => x.NormalizedValue, StringComparer.OrdinalIgnoreCase);
        var toRemove = product.OemNumbers
            .Where(x => !incomingByNormalized.ContainsKey(x.NormalizedOemNumber))
            .ToList();

        if (toRemove.Count > 0)
        {
            _context.ExternalProductOemNumbers.RemoveRange(toRemove);
        }

        foreach (var incoming in incomingOems)
        {
            var existing = product.OemNumbers.FirstOrDefault(x => x.NormalizedOemNumber.Equals(incoming.NormalizedValue, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.OriginalOemNumber = incoming.OriginalValue;
                existing.UpdatedDate = nowUtc;
                continue;
            }

            product.OemNumbers.Add(new ExternalProductOemNumber
            {
                Id = Guid.NewGuid(),
                ExternalProductId = product.Id,
                OriginalOemNumber = incoming.OriginalValue,
                NormalizedOemNumber = incoming.NormalizedValue,
                CreatedDate = nowUtc
            });
        }
    }
}
