using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Application.Features.Chat.Commands.AskChat;

public sealed class AskChatCommandHandler : IRequestHandler<AskChatCommand, OperationResult<AskChatResponse>>
{
    private static readonly Regex PartNumberRegex = new(
        @"\b[A-Z0-9][A-Z0-9-]{4,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IChatQueryService _chatQueryService;
    private readonly ILogger<AskChatCommandHandler> _logger;

    public AskChatCommandHandler(IChatQueryService chatQueryService, ILogger<AskChatCommandHandler> logger)
    {
        _chatQueryService = chatQueryService;
        _logger = logger;
    }

    public async Task<OperationResult<AskChatResponse>> Handle(AskChatCommand request, CancellationToken cancellationToken)
    {
        var (intent, searchTerm, partCode, confidence, multiTerms) = ParseDebugIntent(request.DebugIntentJson);
        var answer = request.AiAnswer;
        var directCodeProducts = await ResolveCodeProductsAsync(
            request,
            partCode,
            searchTerm,
            multiTerms,
            cancellationToken);

        if (confidence.HasValue && confidence.Value < 0.60)
        {
            if (directCodeProducts.Count > 0)
            {
                _logger.LogInformation(
                    "Low intent confidence bypassed with exact code fallback. confidence={Confidence} text={Text}",
                    confidence.Value,
                    request.UserText);

                return Success(
                    replySuggestion: BuildDirectCodeMatchReply(directCodeProducts),
                    products: directCodeProducts,
                    debugInfo: $"Intent: {intent ?? "Yok"} | LowConfidenceCodeFallback: {confidence.Value:0.00}");
            }

            _logger.LogWarning("Low intent confidence: {Confidence} | Intent: {Intent} | Text: {Text}",
                confidence.Value, intent ?? "n/a", request.UserText);

            var clarification = BuildLowConfidenceClarificationMessage(request.DebugIntentJson, request.UserText);
            return Success(
                replySuggestion: clarification,
                products: [],
                debugInfo: $"Intent: {intent ?? "Yok"} | LowConfidence: {confidence.Value:0.00}");
        }

        if (string.Equals(intent, "CHAT", StringComparison.OrdinalIgnoreCase))
        {
            return Success(
                replySuggestion: request.AiAnswer ?? "Buyur ustam?",
                products: [],
                debugInfo: $"Intent: {intent} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}");
        }

        if (string.Equals(intent, "HELP", StringComparison.OrdinalIgnoreCase))
        {
            return Success(
                replySuggestion: "Ustam, hangi bilgiyi istersin? (fiyat, stok, uyumluluk, parça kodu) diye sor.",
                products: [],
                debugInfo: $"Intent: HELP | Confidence: {confidence?.ToString("0.00") ?? "n/a"}");
        }

        if (string.Equals(intent, "DIAGNOSE", StringComparison.OrdinalIgnoreCase))
        {
            var products = await ResolveDiagnoseProductsAsync(
                request,
                partCode,
                searchTerm,
                multiTerms,
                cancellationToken);

            var baseDiagnosis = string.IsNullOrWhiteSpace(request.AiAnswer)
                ? "Belirtiye göre olası nedenleri sıraladım. Net marka-model veya parça kodu verirsen nokta atışı yaparım."
                : request.AiAnswer!;

            var reply = products.Count > 0
                ? $"{baseDiagnosis}\n\nOlası ilgili parçaları da aşağıya ekledim."
                : baseDiagnosis;

            return Success(
                replySuggestion: reply,
                products: products,
                debugInfo: $"Intent: DIAGNOSE | Search: {searchTerm ?? "Yok"} | Code: {partCode ?? "Yok"} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}");
        }

        if (string.Equals(intent, "SEARCH", StringComparison.OrdinalIgnoreCase) && multiTerms.Count > 1)
        {
            if (request.Sources.Count > 0)
            {
                var groups = request.Sources
                    .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                    .GroupBy(s => s.Query ?? string.Empty)
                    .ToList();

                var compareGroups = new List<ChatCompareGroupDto>();
                foreach (var group in groups)
                {
                    var products = await _chatQueryService.EnrichPythonSourcesAsync(group.ToList(), request.CatalogIds, cancellationToken);
                    compareGroups.Add(new ChatCompareGroupDto
                    {
                        Query = string.IsNullOrWhiteSpace(group.Key) ? "Genel" : group.Key,
                        Results = products
                    });
                }

                var anyResults = compareGroups.Any(g => g.Results.Any());
                return Success(
                    replySuggestion: anyResults
                        ? (request.AiAnswer ?? "Birden fazla parça için sonuçları ayrı ayrı listeledim.")
                        : "Birden fazla parça istedin ama uygun sonuç bulamadım.",
                    products: [],
                    compareGroups: compareGroups,
                    debugInfo: $"Intent: SEARCH | Terms: {string.Join(", ", multiTerms)}");
            }

            var fallbackGroups = new List<ChatCompareGroupDto>();
            foreach (var term in multiTerms)
            {
                var results = await _chatQueryService.SearchByCodeAsync(term, request.CatalogIds, cancellationToken);
                var products = await _chatQueryService.EnrichResultsAsync(results, request.CatalogIds, cancellationToken);
                fallbackGroups.Add(new ChatCompareGroupDto
                {
                    Query = term,
                    Results = products
                });
            }

            var anyFallback = fallbackGroups.Any(g => g.Results.Any());
            return Success(
                replySuggestion: anyFallback
                    ? (request.AiAnswer ?? "Birden fazla parça için sonuçları ayrı ayrı listeledim.")
                    : "Birden fazla parça istedin ama uygun sonuç bulamadım.",
                products: [],
                compareGroups: fallbackGroups,
                debugInfo: $"Intent: SEARCH | Terms: {string.Join(", ", multiTerms)}");
        }

        var intentQuery = partCode ?? searchTerm ?? request.UserText;
        if (string.Equals(intent, "PRICE", StringComparison.OrdinalIgnoreCase))
        {
            var products = directCodeProducts.Count > 0
                ? directCodeProducts
                : await ResolveProductsBySingleTermAsync(intentQuery, request.CatalogIds, cancellationToken);

            return products.Count == 0
                ? Success("Fiyat için uygun parça bulamadım. Kod veya isim net mi?", [], $"Intent: PRICE | Code: {intentQuery}")
                : Success(request.AiAnswer ?? $"Fiyat bilgisi bulunan {products.Count} parça buldum.", products, $"Intent: PRICE | Code: {intentQuery}");
        }

        if (string.Equals(intent, "STOCK", StringComparison.OrdinalIgnoreCase))
        {
            var products = directCodeProducts.Count > 0
                ? directCodeProducts
                : await ResolveProductsBySingleTermAsync(intentQuery, request.CatalogIds, cancellationToken);

            return products.Count == 0
                ? Success("Stok için uygun parça bulamadım.", [], $"Intent: STOCK | Code: {intentQuery}")
                : Success(request.AiAnswer ?? "Stok durumlarını listeledim.", products, $"Intent: STOCK | Code: {intentQuery}");
        }

        if (string.Equals(intent, "COMPATIBILITY", StringComparison.OrdinalIgnoreCase))
        {
            var products = directCodeProducts.Count > 0
                ? directCodeProducts
                : await ResolveProductsBySingleTermAsync(intentQuery, request.CatalogIds, cancellationToken);
            return Success(
                replySuggestion: products.Count > 0
                    ? (request.AiAnswer ?? "Uyumlu model bilgilerini listeledim.")
                    : "Uyumluluk için parça bulunamadı.",
                products: products,
                debugInfo: $"Intent: COMPATIBILITY | Code: {intentQuery}");
        }

        if (string.Equals(intent, "COMPARE", StringComparison.OrdinalIgnoreCase))
        {
            var compareQuery = partCode ?? searchTerm ?? request.UserText;
            var terms = ExtractCompareTerms(compareQuery);
            var compareGroups = new List<ChatCompareGroupDto>();

            foreach (var term in terms)
            {
                var results = await _chatQueryService.SearchByCodeAsync(term, request.CatalogIds, cancellationToken);
                var products = await _chatQueryService.EnrichResultsAsync(results, request.CatalogIds, cancellationToken);
                compareGroups.Add(new ChatCompareGroupDto
                {
                    Query = term,
                    Results = products
                });
            }

            return Success(
                replySuggestion: compareGroups.Any()
                    ? "Karşılaştırma için parçaları yan yana listeledim."
                    : "Karşılaştırma için uygun parça bulamadım.",
                products: [],
                compareGroups: compareGroups,
                debugInfo: $"Intent: COMPARE | Terms: {string.Join(", ", terms)}");
        }

        IReadOnlyList<EnrichedPartDto> finalProducts = [];
        if (request.Sources.Count > 0)
        {
            finalProducts = await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(partCode) && IsPartNumber(partCode))
        {
            var fallback = await _chatQueryService.SearchByCodeAsync(partCode, request.CatalogIds, cancellationToken);
            finalProducts = await _chatQueryService.EnrichResultsAsync(fallback, request.CatalogIds, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(searchTerm) && IsPartNumber(searchTerm))
        {
            var fallback = await _chatQueryService.SearchByCodeAsync(searchTerm, request.CatalogIds, cancellationToken);
            finalProducts = await _chatQueryService.EnrichResultsAsync(fallback, request.CatalogIds, cancellationToken);
        }

        if (finalProducts.Count == 0 && directCodeProducts.Count > 0)
        {
            finalProducts = directCodeProducts;
            answer = BuildDirectCodeMatchReply(finalProducts);
        }
        else if (finalProducts.Count > 0 && string.IsNullOrWhiteSpace(answer))
        {
            answer = BuildDirectCodeMatchReply(finalProducts);
        }

        return Success(
            replySuggestion: answer ?? "Üzgünüm, sonuç bulunamadı.",
            products: finalProducts,
            debugInfo: $"Intent: {intent ?? "Yok"} | Search: {searchTerm ?? "Yok"} | Code: {partCode ?? "Yok"} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}");
    }

    private static OperationResult<AskChatResponse> Success(
        string replySuggestion,
        IReadOnlyList<EnrichedPartDto> products,
        string? debugInfo = null,
        IReadOnlyList<ChatCompareGroupDto>? compareGroups = null)
    {
        return OperationResult<AskChatResponse>.Success(new AskChatResponse
        {
            ReplySuggestion = replySuggestion,
            Products = products,
            CompareGroups = compareGroups,
            DebugInfo = debugInfo
        });
    }

    private static (string? Intent, string? SearchTerm, string? PartCode, double? Confidence, List<string> MultiTerms) ParseDebugIntent(string? debugIntentJson)
    {
        if (string.IsNullOrWhiteSpace(debugIntentJson))
        {
            return (null, null, null, null, []);
        }

        try
        {
            using var doc = JsonDocument.Parse(debugIntentJson);
            var root = doc.RootElement;

            string? intent = root.TryGetProperty("intent", out var it) ? it.GetString() : null;
            string? searchTerm = root.TryGetProperty("part_name", out var pn) ? pn.GetString() : null;
            string? partCode = root.TryGetProperty("part_code", out var pc) ? pc.GetString() : null;

            double? confidence = null;
            if (root.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number)
            {
                confidence = cf.GetDouble();
            }

            var multiTerms = ExtractPartsFromDebugIntent(root);
            return (intent, searchTerm, partCode, confidence, multiTerms);
        }
        catch
        {
            return (null, null, null, null, []);
        }
    }

    private static List<string> ExtractPartsFromDebugIntent(JsonElement intentElement)
    {
        var terms = new List<string>();

        if (intentElement.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("part_code", out var pc) && pc.ValueKind == JsonValueKind.String)
                {
                    var value = pc.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        terms.Add(value);
                        continue;
                    }
                }

                if (part.TryGetProperty("part_name", out var pn) && pn.ValueKind == JsonValueKind.String)
                {
                    var value = pn.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        terms.Add(value);
                    }
                }
            }
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractCompareTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var separators = new[] { " ve ", " & ", ",", ";", "/" };
        var parts = separators.Aggregate(new List<string> { text }, (list, sep) =>
            list.SelectMany(x => x.Split(sep, StringSplitOptions.RemoveEmptyEntries)).ToList());

        return parts
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPartNumber(string? term)
    {
        return !string.IsNullOrWhiteSpace(term) && term.Length > 2 && term.Any(char.IsDigit);
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> ResolveProductsBySingleTermAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        var results = await _chatQueryService.SearchByCodeAsync(term, catalogIds, cancellationToken);
        return await _chatQueryService.EnrichResultsAsync(results, catalogIds, cancellationToken);
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> ResolveCodeProductsAsync(
        AskChatCommand request,
        string? partCode,
        string? searchTerm,
        IReadOnlyCollection<string> multiTerms,
        CancellationToken cancellationToken)
    {
        var codeTerms = ExtractPartNumberCandidates(
                [partCode, searchTerm, request.UserText, .. multiTerms])
            .ToList();

        if (codeTerms.Count == 0)
        {
            return [];
        }

        var allItems = new List<Katalogcu.Domain.Entities.CatalogItem>();
        foreach (var term in codeTerms)
        {
            var results = await _chatQueryService.SearchByCodeAsync(term, request.CatalogIds, cancellationToken);
            allItems.AddRange(results);
        }

        var enriched = await _chatQueryService.EnrichResultsAsync(allItems, request.CatalogIds, cancellationToken);
        return DeduplicateProducts(enriched);
    }

    private static IReadOnlyList<string> ExtractPartNumberCandidates(IEnumerable<string?> values)
    {
        var candidates = new List<string>();

        foreach (var rawValue in values)
        {
            var value = rawValue?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsPartNumber(value) && !value.Any(char.IsWhiteSpace) && value.Length <= 64)
            {
                candidates.Add(value);
            }

            foreach (Match match in PartNumberRegex.Matches(value))
            {
                var token = match.Value.Trim();
                if (IsPartNumber(token))
                {
                    candidates.Add(token);
                }
            }
        }

        return candidates
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static string BuildDirectCodeMatchReply(IReadOnlyCollection<EnrichedPartDto> products)
    {
        var codes = products
            .Select(p => p.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (codes.Count == 0)
        {
            return $"Veritabanında {products.Count} uygun parça buldum.";
        }

        return codes.Count == 1
            ? $"{codes[0]} kodlu parçayı buldum."
            : $"{string.Join(", ", codes)} kodlu parçaları buldum.";
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> ResolveDiagnoseProductsAsync(
        AskChatCommand request,
        string? partCode,
        string? searchTerm,
        IReadOnlyCollection<string> multiTerms,
        CancellationToken cancellationToken)
    {
        if (request.Sources.Count > 0)
        {
            var sourced = await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken);
            var dedupSourced = DeduplicateProducts(sourced);
            if (dedupSourced.Count > 0)
            {
                return dedupSourced;
            }
        }

        var candidateCodeTerms = new List<string>();
        if (IsPartNumber(partCode))
        {
            candidateCodeTerms.Add(partCode!);
        }

        if (IsPartNumber(searchTerm))
        {
            candidateCodeTerms.Add(searchTerm!);
        }

        candidateCodeTerms.AddRange(multiTerms.Where(IsPartNumber));

        var distinctCodeTerms = candidateCodeTerms
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctCodeTerms.Count > 0)
        {
            var allItems = new List<Katalogcu.Domain.Entities.CatalogItem>();
            foreach (var term in distinctCodeTerms)
            {
                var results = await _chatQueryService.SearchByCodeAsync(term, request.CatalogIds, cancellationToken);
                allItems.AddRange(results);
            }

            var enrichedFromCode = await _chatQueryService.EnrichResultsAsync(allItems, request.CatalogIds, cancellationToken);
            var dedupFromCode = DeduplicateProducts(enrichedFromCode);
            if (dedupFromCode.Count > 0)
            {
                return dedupFromCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm) && request.Sources.Count > 0)
        {
            var sourceMatchesByName = request.Sources
                .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                .Where(s =>
                    (!string.IsNullOrWhiteSpace(s.Name) && s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(s.Query) && s.Query.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (sourceMatchesByName.Count > 0)
            {
                var enrichedByNameMatch = await _chatQueryService.EnrichPythonSourcesAsync(sourceMatchesByName, request.CatalogIds, cancellationToken);
                return DeduplicateProducts(enrichedByNameMatch);
            }
        }

        return [];
    }

    private static IReadOnlyList<EnrichedPartDto> DeduplicateProducts(IReadOnlyCollection<EnrichedPartDto> products)
    {
        return products
            .Where(p => !string.IsNullOrWhiteSpace(p.Code) || p.Id != Guid.Empty)
            .GroupBy(
                p => !string.IsNullOrWhiteSpace(p.Code)
                    ? p.Code.Trim().ToUpperInvariant()
                    : p.Id.ToString(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(8)
            .ToList();
    }

    private static string BuildLowConfidenceClarificationMessage(string? debugIntentJson, string? userText)
    {
        string? brand = null;
        string? machineModel = null;
        string? machineGroup = null;
        string? partCode = null;

        if (!string.IsNullOrWhiteSpace(debugIntentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(debugIntentJson);
                var root = doc.RootElement;
                brand = root.TryGetProperty("brand", out var b) ? b.GetString() : null;
                machineModel = root.TryGetProperty("machine_model", out var mm) ? mm.GetString() : null;
                machineGroup = root.TryGetProperty("machine_group", out var mg) ? mg.GetString() : null;
                partCode = root.TryGetProperty("part_code", out var pc) ? pc.GetString() : null;
            }
            catch
            {
                // no-op
            }
        }

        var questions = new List<string>();
        if (string.IsNullOrWhiteSpace(brand))
            questions.Add("Makine markası nedir? (örn: Yamato/Juki)");
        if (string.IsNullOrWhiteSpace(machineModel))
            questions.Add("Makine modeli nedir? (örn: MO-3704, VG2500-8F)");
        if (string.IsNullOrWhiteSpace(machineGroup))
            questions.Add("Makine tipi nedir? (Düz dikiş / Overlok / Reçme)");
        if (string.IsNullOrWhiteSpace(partCode))
            questions.Add("Parça kodu veya net ölçü var mı? (örn: M3-0.5x3)");

        if (questions.Count == 0)
            questions.Add("Parçanın fotoğrafını veya eski kodunu paylaşır mısın?");

        return
            "Ustam, mesajını büyük ölçüde anladım ama yanlış parça önermemek için netleştirelim:\n- "
            + string.Join("\n- ", questions.Take(3));
    }
}
