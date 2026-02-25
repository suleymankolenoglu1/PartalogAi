using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Chat.Common;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class ChatQueryService : IChatQueryService
{
    private readonly AppDbContext _context;

    public ChatQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Guid>> ResolveAccessibleCatalogIdsAsync(
        Guid tokenUserId,
        Guid? publicUserId,
        IReadOnlyCollection<Guid>? publicAllowedCatalogIds,
        IReadOnlyCollection<Guid> requestedCatalogIds,
        CancellationToken cancellationToken)
    {
        if (tokenUserId != Guid.Empty)
        {
            var userQuery = _context.Catalogs.AsNoTracking().Where(c => c.UserId == tokenUserId);
            if (requestedCatalogIds.Count > 0)
            {
                userQuery = userQuery.Where(c => requestedCatalogIds.Contains(c.Id));
            }

            return await userQuery.Select(c => c.Id).ToListAsync(cancellationToken);
        }

        if (publicUserId.HasValue && publicUserId.Value != Guid.Empty)
        {
            var publicQuery = _context.Catalogs.AsNoTracking()
                .Where(c => c.Status == "Published" && c.UserId == publicUserId.Value);

            if (publicAllowedCatalogIds is { Count: > 0 })
            {
                publicQuery = publicQuery.Where(c => publicAllowedCatalogIds.Contains(c.Id));
            }

            if (requestedCatalogIds.Count > 0)
            {
                publicQuery = publicQuery.Where(c => requestedCatalogIds.Contains(c.Id));
            }

            return await publicQuery.Select(c => c.Id).ToListAsync(cancellationToken);
        }

        return [];
    }

    public async Task<IReadOnlyList<EnrichedPartDto>> EnrichPythonSourcesAsync(
        IReadOnlyCollection<ChatSourceInput> sources,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        var codes = sources
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .Select(s => s.Code!.Trim())
            .Distinct()
            .ToList();

        if (codes.Count == 0 || catalogIds.Count == 0)
        {
            return [];
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => codes.Contains(p.Code) && catalogIds.Contains(p.CatalogId))
            .ToListAsync(cancellationToken);

        var productDict = products.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First());

        var catalogItems = await _context.CatalogItems
            .AsNoTracking()
            .Where(ci => codes.Contains(ci.PartCode) && catalogIds.Contains(ci.CatalogId))
            .ToListAsync(cancellationToken);

        var itemDict = catalogItems
            .GroupBy(ci => ci.PartCode)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.PartName) && x.PartName != "Unknown Part")
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Description))
                .First());

        var enrichedList = new List<EnrichedPartDto>();

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Code))
            {
                continue;
            }

            var sourceCode = source.Code.Trim();
            productDict.TryGetValue(sourceCode, out var product);
            itemDict.TryGetValue(sourceCode, out var catItem);

            var sourceModel = source.Model ?? source.LegacyModel;
            var sourceDesc = source.Description ?? source.LegacyDescription;

            string? finalName = catItem?.PartName;
            if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = source.Name;
            if ((string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") && !string.IsNullOrWhiteSpace(catItem?.Description)) finalName = catItem.Description;
            if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = $"Parça {sourceCode}";

            enrichedList.Add(new EnrichedPartDto
            {
                Id = catItem?.Id ?? Guid.Empty,
                Code = sourceCode,
                Name = finalName,
                Description = catItem?.Description ?? sourceDesc,
                Model = sourceModel,
                CatalogId = catItem?.CatalogId ?? Guid.Empty,
                PageNumber = catItem?.PageNumber,
                StockStatus = product != null ? "Stokta Var" : "Stokta Yok",
                Price = product?.Price,
                ImageUrl = !string.IsNullOrWhiteSpace(catItem?.VisualImageUrl) ? catItem.VisualImageUrl : product?.ImageUrl
            });
        }

        return enrichedList;
    }

    public async Task<IReadOnlyList<CatalogItem>> SearchByCodeAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term) || catalogIds.Count == 0)
        {
            return [];
        }

        var code = term.Trim().ToUpperInvariant();
        return await _context.CatalogItems
            .AsNoTracking()
            .Where(ci =>
                catalogIds.Contains(ci.CatalogId) &&
                (ci.RefNumber == code || ci.PartCode == code || ci.PartCode.StartsWith(code)))
            .OrderBy(ci => ci.PartCode.Length)
            .Take(5)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnrichedPartDto>> EnrichResultsAsync(
        IReadOnlyCollection<CatalogItem> items,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || catalogIds.Count == 0)
        {
            return [];
        }

        var codes = items.Select(i => i.PartCode).Distinct().ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => codes.Contains(p.Code) && catalogIds.Contains(p.CatalogId))
            .ToListAsync(cancellationToken);

        var productDict = products.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First());

        var cleanCatalogItems = await _context.CatalogItems
            .AsNoTracking()
            .Where(ci => codes.Contains(ci.PartCode) && catalogIds.Contains(ci.CatalogId))
            .ToListAsync(cancellationToken);

        var bestItemsDict = cleanCatalogItems
            .GroupBy(ci => ci.PartCode)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.PartName) && x.PartName != "Unknown Part")
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Description))
                .First());

        return items.Select(item =>
        {
            var partCode = item.PartCode ?? string.Empty;
            productDict.TryGetValue(partCode, out var product);
            bestItemsDict.TryGetValue(partCode, out var bestItem);
            var targetItem = bestItem ?? item;

            string displayName = targetItem.PartName;
            if (string.IsNullOrWhiteSpace(displayName) || displayName == "Unknown Part")
            {
                displayName = !string.IsNullOrWhiteSpace(targetItem.Description) ? targetItem.Description : $"Parça {targetItem.PartCode}";
            }

            return new EnrichedPartDto
            {
                Id = targetItem.Id,
                Code = partCode,
                Name = displayName,
                Description = targetItem.Description,
                CatalogId = targetItem.CatalogId,
                PageNumber = targetItem.PageNumber,
                StockStatus = product != null ? "Stokta Var" : "Stokta Yok",
                Price = product?.Price,
                ImageUrl = !string.IsNullOrWhiteSpace(targetItem.VisualImageUrl) ? targetItem.VisualImageUrl : product?.ImageUrl
            };
        }).ToList();
    }
}
