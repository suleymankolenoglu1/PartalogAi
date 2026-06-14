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
            var sourceCatalogId = source.CatalogId.HasValue && catalogIds.Contains(source.CatalogId.Value)
                ? source.CatalogId.Value
                : Guid.Empty;
            var sourcePageNumber = string.IsNullOrWhiteSpace(source.PageNumber) ? "1" : source.PageNumber.Trim();
            var resolvedCatalogItemPage = ResolveViewerPageNumber(catItem);
            string? finalName = catItem?.PartName;
            if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = source.Name;
            if ((string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") && !string.IsNullOrWhiteSpace(catItem?.Description)) finalName = catItem.Description;
            if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = $"Parça {sourceCode}";

            enrichedList.Add(new EnrichedPartDto
            {
                Id = catItem?.Id ?? Guid.Empty,
                Code = sourceCode,
                RefNumber = catItem?.RefNumber,
                Name = finalName,
                Description = catItem?.Description ?? sourceDesc,
                Model = sourceModel,
                Brand = catItem?.MachineBrand,
                CatalogId = catItem?.CatalogId ?? sourceCatalogId,
                PageNumber = resolvedCatalogItemPage ?? sourcePageNumber,
                StockStatus = null,
                Price = null,
                ImageUrl = !string.IsNullOrWhiteSpace(catItem?.VisualImageUrl) ? catItem.VisualImageUrl : product?.ImageUrl,
                Quantity = product?.StockQuantity ?? 0,
                SourceQuery = source.Query,
                SourceSimilarity = source.Similarity,
                MatchReason = source.MatchReason,
                ConfidenceLabel = source.ConfidenceLabel,
                RequiresVerification = source.RequiresVerification,
                Fallback = source.Fallback,
                FallbackReason = source.FallbackReason,
                CompatibilityLevel = null,
                CompatibilitySourceType = null,
                CompatibilityConfidence = null,
                CompatibilityMachineLabel = null,
                CompatibilityNotes = null
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
            .OrderByDescending(ci => ci.PartCode == code)
            .ThenByDescending(ci => ci.RefNumber == code)
            .ThenByDescending(ci => ci.PartCode.StartsWith(code))
            .ThenBy(ci => ci.PartCode.Length)
            .Take(5)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> SearchByRefNumberAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term) || catalogIds.Count == 0)
        {
            return [];
        }

        var reference = term.Trim().ToUpperInvariant();
        return await _context.CatalogItems
            .AsNoTracking()
            .Where(ci =>
                catalogIds.Contains(ci.CatalogId) &&
                (ci.RefNumber == reference || ci.RefNumber.StartsWith(reference)))
            .OrderByDescending(ci => ci.RefNumber == reference)
            .ThenBy(ci => ci.RefNumber.Length)
            .Take(5)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> SearchByNameAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term) || catalogIds.Count == 0)
        {
            return [];
        }

        var normalized = term.Trim();
        var tokens = ExtractSearchTokens(normalized);
        if (tokens.Count == 0)
        {
            return [];
        }

        var query = _context.CatalogItems
            .AsNoTracking()
            .Where(ci => catalogIds.Contains(ci.CatalogId));

        query = ApplyNameSearchFilters(query, tokens);

        var candidates = await query
            .Take(120)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        return candidates
            .Select(ci => new
            {
                Item = ci,
                Score = ScoreNameMatch(ci, normalized, tokens)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.PartName)
            .Take(8)
            .Select(x => x.Item)
            .ToList();
    }

    internal static IQueryable<CatalogItem> ApplyNameSearchFilters(
        IQueryable<CatalogItem> query,
        IReadOnlyList<NameSearchToken> tokens)
    {
        // AND logic: every meaningful token must match at least one field on the same record.
        // Raw and normalized variants keep Turkish text working while still matching ASCII catalog data.
        foreach (var token in tokens)
        {
            var primaryPattern = BuildContainsLikePattern(token.Raw);
            var normalizedPattern = token.Normalized.Equals(token.Raw, StringComparison.OrdinalIgnoreCase)
                ? null
                : BuildContainsLikePattern(token.Normalized);
            query = query.Where(ci =>
                EF.Functions.ILike(ci.PartName, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.Description, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.SearchText ?? string.Empty, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.PartCode, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.RefNumber, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.MachineModel ?? string.Empty, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.MachineBrand ?? string.Empty, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.MachineGroup ?? string.Empty, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.Mechanism ?? string.Empty, primaryPattern, "\\") ||
                EF.Functions.ILike(ci.Dimensions ?? string.Empty, primaryPattern, "\\") ||
                (normalizedPattern != null && (
                    EF.Functions.ILike(ci.PartName, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.Description, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.SearchText ?? string.Empty, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.PartCode, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.RefNumber, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.MachineModel ?? string.Empty, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.MachineBrand ?? string.Empty, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.MachineGroup ?? string.Empty, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.Mechanism ?? string.Empty, normalizedPattern, "\\") ||
                    EF.Functions.ILike(ci.Dimensions ?? string.Empty, normalizedPattern, "\\")))
            );
        }

        return query;
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
                RefNumber = targetItem.RefNumber,
                Name = displayName,
                Description = targetItem.Description,
                Model = targetItem.MachineModel,
                Brand = targetItem.MachineBrand,
                CatalogId = targetItem.CatalogId,
                PageNumber = ResolveViewerPageNumber(targetItem),
                StockStatus = null,
                Price = null,
                ImageUrl = !string.IsNullOrWhiteSpace(targetItem.VisualImageUrl) ? targetItem.VisualImageUrl : product?.ImageUrl,
                CompatibilityLevel = null,
                CompatibilitySourceType = null,
                CompatibilityConfidence = null,
                CompatibilityMachineLabel = null,
                CompatibilityNotes = null
            };
        }).ToList();
    }

    private static string ResolveViewerPageNumber(CatalogItem? item)
    {
        if (item is null)
        {
            return "1";
        }

        if (item.VisualPageNumber.HasValue && item.VisualPageNumber.Value > 0)
        {
            return item.VisualPageNumber.Value.ToString();
        }

        return string.IsNullOrWhiteSpace(item.PageNumber) ? "1" : item.PageNumber;
    }

    internal static IReadOnlyList<NameSearchToken> ExtractSearchTokens(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // === EXISTING (preserved) ===
            "BU", "BUNU", "BUNUN",
            "HANGI", "HANGİ",
            "KOD", "KODU", "KODLU",
            "PARCA", "PARÇA",
            "NEDIR", "NEDİR", "NE",
            "MI", "Mİ", "MU", "MÜ",
            "VAR",
            "BUL", "GOSTER", "GÖSTER",

            // === PRONOUNS / PERSONAL ===
            "BEN", "SEN", "O", "BIZ", "SIZ", "ONLAR",
            "BANA", "SANA", "ONA", "BIZE", "SIZE",
            "KENDI", "KENDİ",

            // === QUESTION WORDS ===
            "NASIL", "NEDEN", "NİYE", "NIYE", "KAC", "KAÇ",

            // === EXISTENCE / MODAL ===
            "LAZIM", "GEREK", "GEREKLI", "GEREKLİ",
            "VAR", "YOK",
            "VARMI", "VARMİ", "YOKMU", "YOKMİ",
            "VARSA", "YOKSA",

            // === DOMAIN-GENERIC (high frequency, zero signal) ===
            "MAKINE", "MAKİNE", "MAKINESI", "MAKİNESİ",
            "YEDEK", "CIHAZ", "CİHAZ",

            // === VERBS (request/find) ===
            "BULUN", "GETIR", "GETİR",
            "ISTER", "ISTE", "ISTIYOR", "ISTERIM", "ISTERİM",
            "VER", "AL", "BAK", "YAP", "ARA",

            // === PREPOSITIONS / CONJUNCTIONS ===
            "ICIN", "İÇİN",
            "ILE", "İLE",
            "VE", "VEYA",
            "AMA", "FAKAT", "ANCAK",
            "GIBI", "GİBİ",
            "KADAR",
            "ILE", "İLE",
            "DA", "DE", "DEN",

            // === QUANTIFIERS / ADVERBS ===
            "BIR", "BİR",
            "IKI", "İKİ",
            "DAHA", "EN", "COK", "ÇOK", "AZ",
            "TEK", "BIRAKIN",

            // === COMMON FILLER ===
            "LUTFEN", "LÜTFEN",
            "EVET", "HAYIR", "BELKI", "BELKİ", "ACABA",
            "SADECE", "AYRICA",
            "SONRA", "ONCE", "ÖNCE", "SIMDI", "ŞİMDİ",
            "BUGUN", "BUGÜN", "YARIN", "DUN", "DÜN",
            "HENUZ", "HENÜZ",
            "LINK", "LİNK",
        };

        return text
            .Split([' ', '-', '/', ',', ';', '_', ':', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(raw => new NameSearchToken(raw, NormalizeForSearch(raw)))
            .Where(token => token.Normalized.Length >= 2)
            .Where(token => !token.IsShortNumericNoise)
            .Where(token => !stopWords.Contains(token.Normalized))
            .DistinctBy(token => token.Normalized, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    internal static int ScoreNameMatch(CatalogItem item, string query, IReadOnlyList<NameSearchToken> tokens)
    {
        var normalizedQuery = NormalizeForSearch(query);
        var normalizedPartName = NormalizeForSearch(item.PartName);
        var normalizedDescription = NormalizeForSearch(item.Description);
        var normalizedSearchText = NormalizeForSearch(item.SearchText);
        var normalizedCode = NormalizeForSearch(item.PartCode);
        var normalizedRef = NormalizeForSearch(item.RefNumber);
        var haystack = NormalizeForSearch(
            $"{item.PartName} {item.Description} {item.SearchText} {item.PartCode} {item.RefNumber} {item.MachineBrand} {item.MachineModel} {item.MachineGroup} {item.Mechanism} {item.Dimensions}");

        if (tokens.Count > 1 && !tokens.All(token => haystack.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        var score = 0;
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            if (normalizedPartName == normalizedQuery) score += 120;
            if (normalizedPartName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) score += 80;
            if (normalizedDescription.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) score += 50;
            if (normalizedSearchText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) score += 45;
        }

        if (tokens.Count > 0 && tokens.All(token => haystack.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)))
        {
            score += 60;
        }

        foreach (var token in tokens)
        {
            if (normalizedPartName.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)) score += 15;
            if (normalizedDescription.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)) score += 10;
            if (normalizedSearchText.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)) score += 10;
            if (normalizedCode.Contains(token.Normalized, StringComparison.OrdinalIgnoreCase)) score += 8;
            if (normalizedRef == token.Normalized) score += 8;
        }

        return score;
    }

    internal static string BuildContainsLikePattern(string token)
    {
        return $"%{EscapeLikePattern(token)}%";
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant()
            .Replace('İ', 'I')
            .Replace('I', 'I')
            .Replace('ı', 'I')
            .Replace('Ş', 'S')
            .Replace('Ğ', 'G')
            .Replace('Ü', 'U')
            .Replace('Ö', 'O')
            .Replace('Ç', 'C')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('/', ' ')
            .Replace('=', ' ');

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    internal readonly record struct NameSearchToken(string Raw, string Normalized)
    {
        public bool IsShortNumericNoise => Normalized.Length < 3 && Normalized.All(char.IsDigit);
    }
}
