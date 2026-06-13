using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Application.Features.Chat.Commands.AskChat;

public sealed class AskChatCommandHandler : IRequestHandler<AskChatCommand, OperationResult<AskChatResponse>>
{
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
        var hasAiSources = request.Sources.Count > 0;

        if (confidence.HasValue && confidence.Value < 0.60 && !hasAiSources && !IsPartNumber(request.UserText))
        {
            _logger.LogWarning("Low intent confidence: {Confidence} | Intent: {Intent} | Text: {Text}",
                confidence.Value, intent ?? "n/a", request.UserText);

            var clarification = BuildLowConfidenceClarificationMessage(request.DebugIntentJson, request.UserText);
            return Success(
                replySuggestion: clarification,
                products: [],
                debugInfo: $"Intent: {intent ?? "Yok"} | LowConfidence: {confidence.Value:0.00}");
        }

        if (string.Equals(intent, "CHAT", StringComparison.OrdinalIgnoreCase) && !hasAiSources)
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
            var products = hasAiSources
                ? await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken)
                : await ResolveProductsByIntentQueryAsync(intentQuery, request.CatalogIds, cancellationToken);

            return products.Count == 0
                ? Success("Fiyat için uygun parça bulamadım. Kod veya isim net mi?", [], $"Intent: PRICE | Code: {intentQuery}")
                : Success(ChooseReply(request.AiAnswer, products, "PRICE"), products, $"Intent: PRICE | Code: {intentQuery}");
        }

        if (string.Equals(intent, "STOCK", StringComparison.OrdinalIgnoreCase))
        {
            var products = hasAiSources
                ? await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken)
                : await ResolveProductsByIntentQueryAsync(intentQuery, request.CatalogIds, cancellationToken);

            return products.Count == 0
                ? Success("Stok için uygun parça bulamadım.", [], $"Intent: STOCK | Code: {intentQuery}")
                : Success(ChooseReply(request.AiAnswer, products, "STOCK"), products, $"Intent: STOCK | Code: {intentQuery}");
        }

        if (string.Equals(intent, "COMPATIBILITY", StringComparison.OrdinalIgnoreCase))
        {
            var products = hasAiSources
                ? await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken)
                : await ResolveProductsByIntentQueryAsync(intentQuery, request.CatalogIds, cancellationToken);
            return Success(
                replySuggestion: products.Count > 0
                    ? BuildCompatibilityReply(products)
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
            var sourcedProducts = await _chatQueryService.EnrichPythonSourcesAsync(request.Sources, request.CatalogIds, cancellationToken);
            var sourceQuery = request.Sources
                .Select(s => s.Query)
                .FirstOrDefault(q => !string.IsNullOrWhiteSpace(q));
            var supplementalProducts = await ResolveSupplementalNameProductsAsync(
                FirstNonEmpty(request.UserText, searchTerm, sourceQuery),
                request.CatalogIds,
                cancellationToken);

            finalProducts = sourcedProducts.Count > 0
                ? DeduplicateProducts(sourcedProducts.Concat(supplementalProducts).ToList())
                : supplementalProducts;
        }
        else if (!string.IsNullOrWhiteSpace(partCode) && IsPartNumber(partCode))
        {
            finalProducts = await SearchByIdentifierAsync(partCode, request.CatalogIds, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(searchTerm) && IsPartNumber(searchTerm))
        {
            finalProducts = await SearchByIdentifierAsync(searchTerm, request.CatalogIds, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            finalProducts = await ResolveProductsByIntentQueryAsync(searchTerm, request.CatalogIds, cancellationToken);
        }

        var userTextNameProducts = await ResolveSupplementalNameProductsAsync(
            request.UserText,
            request.CatalogIds,
            cancellationToken);
        if (userTextNameProducts.Count > 0)
        {
            var preferUserTextNameProducts =
                finalProducts.Count == 0 ||
                HasStrongNameHit(request.UserText, userTextNameProducts[0]);

            finalProducts = preferUserTextNameProducts
                ? DeduplicateProducts(userTextNameProducts.Concat(finalProducts).ToList())
                : DeduplicateProducts(finalProducts.Concat(userTextNameProducts).ToList());
        }

        if (IsPartNumber(request.UserText) && finalProducts.Count == 0)
        {
            var direct = await SearchByIdentifierAsync(request.UserText, request.CatalogIds, cancellationToken);
            if (direct.Count > 0)
            {
                finalProducts = direct;
                answer = $"Aradığınız {request.UserText} kodlu ürün için veritabanında {finalProducts.Count} sonuç buldum.";
            }
        }

        var hasExactIdentifierHit = HasExactIdentifierHit(request.UserText, partCode, finalProducts);

        return Success(
            replySuggestion: finalProducts.Count > 0
                ? BuildFinalReply(answer, finalProducts, intent, hasExactIdentifierHit, request.UserText)
                : (answer ?? "Üzgünüm, sonuç bulunamadı."),
            products: finalProducts,
            debugInfo: $"Intent: {intent ?? "Yok"} | Search: {searchTerm ?? "Yok"} | Code: {partCode ?? "Yok"} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}");
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> ResolveProductsByIntentQueryAsync(
        string? intentQuery,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Katalogcu.Domain.Entities.CatalogItem> results = IsPartNumber(intentQuery)
            ? await ResolveIdentifierItemsAsync(intentQuery, catalogIds, cancellationToken)
            : await _chatQueryService.SearchByNameAsync(intentQuery, catalogIds, cancellationToken);
        return await _chatQueryService.EnrichResultsAsync(results, catalogIds, cancellationToken);
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> SearchByIdentifierAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        var merged = await ResolveIdentifierItemsAsync(term, catalogIds, cancellationToken);
        return await _chatQueryService.EnrichResultsAsync(merged, catalogIds, cancellationToken);
    }

    private async Task<IReadOnlyList<Katalogcu.Domain.Entities.CatalogItem>> ResolveIdentifierItemsAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        var codeResults = await _chatQueryService.SearchByCodeAsync(term, catalogIds, cancellationToken);
        var refResults = await _chatQueryService.SearchByRefNumberAsync(term, catalogIds, cancellationToken);

        return codeResults
            .Concat(refResults)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .Take(8)
            .ToList();
    }

    private async Task<IReadOnlyList<EnrichedPartDto>> ResolveSupplementalNameProductsAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        if (!LooksLikeNameQuery(term))
        {
            return [];
        }

        var results = await _chatQueryService.SearchByNameAsync(term, catalogIds, cancellationToken);
        return await _chatQueryService.EnrichResultsAsync(results, catalogIds, cancellationToken);
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

    private static bool LooksLikeNameQuery(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        var normalized = term.Trim();
        return normalized.Length >= 2 && normalized.Any(char.IsLetter);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string NormalizeLookupToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToUpperInvariant()
            .Select(ch => ch switch
            {
                'İ' or 'I' or 'ı' => 'I',
                'Ş' => 'S',
                'Ğ' => 'G',
                'Ü' => 'U',
                'Ö' => 'O',
                'Ç' => 'C',
                _ => ch
            })
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private static IReadOnlyList<string> ExtractLookupTokens(string? userText, string? partCode)
    {
        var tokens = new List<string>();

        void AddToken(string? raw)
        {
            var normalized = NormalizeLookupToken(raw);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                normalized.Any(char.IsDigit) &&
                !tokens.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                tokens.Add(normalized);
            }
        }

        AddToken(partCode);

        if (!string.IsNullOrWhiteSpace(userText))
        {
            foreach (var chunk in userText.Split([' ', ',', ';', '/', '\\', ':', '(', ')', '[', ']', '{', '}', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddToken(chunk);
            }
        }

        return tokens;
    }

    private static bool HasExactIdentifierHit(
        string? userText,
        string? partCode,
        IReadOnlyList<EnrichedPartDto> products)
    {
        if (products.Count == 0)
        {
            return false;
        }

        var tokens = ExtractLookupTokens(userText, partCode);
        if (tokens.Count == 0)
        {
            return false;
        }

        return products.Any(product =>
        {
            var codeToken = NormalizeLookupToken(product.Code);
            var refToken = NormalizeLookupToken(product.RefNumber);
            return tokens.Contains(codeToken, StringComparer.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(refToken) && tokens.Contains(refToken, StringComparer.OrdinalIgnoreCase));
        });
    }

    private static bool HasStrongNameHit(string? userText, EnrichedPartDto product)
    {
        if (string.IsNullOrWhiteSpace(userText) || string.IsNullOrWhiteSpace(product.Name))
        {
            return false;
        }

        var normalizedUserText = NormalizeLookupToken(userText);
        var normalizedName = NormalizeLookupToken(product.Name);
        if (normalizedName.Length >= 4 && normalizedUserText.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var nameTokens = product.Name
            .Split([' ', '-', '/', ',', ';', '_', '=', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return nameTokens.Count > 0 &&
               nameTokens.All(token => normalizedUserText.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCompatibilityHint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        var hints = new[]
        {
            "hangi model",
            "hangi makine",
            "uyumlu",
            "uyar mı",
            "hangi cihaz",
            "hangi seri"
        };

        return hints.Any(hint => normalized.Contains(hint, StringComparison.OrdinalIgnoreCase));
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
            questions.Add("Makine modeli nedir? Model etiketindeki tam adı yazabilir misin?");
        if (string.IsNullOrWhiteSpace(machineGroup))
            questions.Add("Makine tipi nedir? (Düz dikiş / Overlok / Reçme)");
        if (string.IsNullOrWhiteSpace(partCode))
            questions.Add("Parça kodu veya net ölçü var mı? Çap, uzunluk veya diş ölçüsü yeterli olur.");

        if (questions.Count == 0)
            questions.Add("Parçanın fotoğrafını veya eski kodunu paylaşır mısın?");

        return
            "Ustam, mesajını büyük ölçüde anladım ama yanlış parça önermemek için netleştirelim:\n- "
            + string.Join("\n- ", questions.Take(3));
    }

    private static string ChooseReply(
        string? aiAnswer,
        IReadOnlyList<EnrichedPartDto> products,
        string? intent)
    {
        if (products.Count == 0)
        {
            return aiAnswer ?? "Sonuç bulunamadı.";
        }

        if (!ShouldOverrideAiAnswer(aiAnswer))
        {
            return aiAnswer!;
        }

        var first = products[0];
        var productName = string.IsNullOrWhiteSpace(first.Name) ? "parça" : first.Name.Trim();
        var code = string.IsNullOrWhiteSpace(first.Code) ? "-" : first.Code.Trim();
        var stock = string.IsNullOrWhiteSpace(first.StockStatus) ? "Stok bilgisi belirsiz" : first.StockStatus.Trim();
        var price = first.Price.HasValue ? $"{first.Price.Value:0.##}" : null;
        var page = string.IsNullOrWhiteSpace(first.PageNumber) ? null : first.PageNumber.Trim();

        return (intent ?? string.Empty).ToUpperInvariant() switch
        {
            "PRICE" => price is not null
                ? $"Ustam, en güçlü eşleşme {productName} ({code}). Fiyat bilgisi {price}. Listede diğer uygun parçaları da ekledim."
                : $"Ustam, en güçlü eşleşme {productName} ({code}). Fiyat alanı boş görünüyor ama uygun parçaları listede gösterdim.",
            "STOCK" => $"Ustam, en güçlü eşleşme {productName} ({code}). Stok durumu: {stock}. Diğer uygun sonuçları da aşağıda görebilirsin.",
            "COMPATIBILITY" => BuildCompatibilityReply(products),
            _ => page is not null
                ? $"Ustam, en güçlü eşleşme {productName} ({code}). Katalogda özellikle {page}. sayfa bağlamından gelen uygun sonuçları aşağıya ekledim."
                : $"Ustam, en güçlü eşleşme {productName} ({code}). Uygun sonuçları aşağıya ekledim."
        };
    }

    private static string BuildFinalReply(
        string? aiAnswer,
        IReadOnlyList<EnrichedPartDto> products,
        string? intent,
        bool hasExactIdentifierHit,
        string? userText)
    {
        if (products.Count == 0)
        {
            return aiAnswer ?? "Üzgünüm, sonuç bulunamadı.";
        }

        if (IsUnavailableFeatureAnswer(aiAnswer))
        {
            return aiAnswer!;
        }

        if (string.Equals(intent, "COMPATIBILITY", StringComparison.OrdinalIgnoreCase) ||
            ContainsCompatibilityHint(userText))
        {
            return BuildCompatibilityReply(products);
        }

        if ((hasExactIdentifierHit || HasStrongNameHit(userText, products[0])) &&
            !string.Equals(intent, "PRICE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(intent, "STOCK", StringComparison.OrdinalIgnoreCase))
        {
            return BuildExactIdentifierReply(products);
        }

        return ChooseReply(aiAnswer, products, intent);
    }

    private static string BuildExactIdentifierReply(IReadOnlyList<EnrichedPartDto> products)
    {
        var first = products[0];
        var code = string.IsNullOrWhiteSpace(first.Code) ? "-" : first.Code.Trim();
        var refNo = string.IsNullOrWhiteSpace(first.RefNumber) ? null : first.RefNumber.Trim();
        var name = string.IsNullOrWhiteSpace(first.Name) ? "parça" : first.Name.Trim();

        var pageHints = products
            .Select(p => string.IsNullOrWhiteSpace(p.PageNumber) ? null : $"Sf {p.PageNumber!.Trim()}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var modelHints = products
            .Select(p =>
            {
                var brand = string.IsNullOrWhiteSpace(p.Brand) ? null : p.Brand.Trim();
                var model = string.IsNullOrWhiteSpace(p.Model) ? null : p.Model.Trim();
                if (brand is null && model is null)
                {
                    return null;
                }

                return brand is not null && model is not null
                    ? $"{brand} {model}"
                    : brand ?? model;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var refText = refNo is not null ? $" Ref no: {refNo}." : string.Empty;
        var pageText = pageHints.Count > 0 ? $" Kaynak: {string.Join(", ", pageHints)}." : string.Empty;

        if (modelHints.Count > 0)
        {
            return $"Ustam, {code} kodlu {name} bulundu.{refText} Geçtiği model/makine bağlamları: {string.Join(", ", modelHints)}.{pageText}";
        }

        return $"Ustam, {code} kodlu {name} bulundu.{refText}{pageText}";
    }

    private static string BuildCompatibilityReply(IReadOnlyList<EnrichedPartDto> products)
    {
        var first = products[0];
        var code = string.IsNullOrWhiteSpace(first.Code) ? "-" : first.Code.Trim();
        var name = string.IsNullOrWhiteSpace(first.Name) ? "parça" : first.Name.Trim();

        var strongestRule = products
            .Where(p => !string.IsNullOrWhiteSpace(p.CompatibilityLevel))
            .OrderBy(p => CompatibilityPriority(p.CompatibilityLevel))
            .ThenByDescending(p => p.CompatibilityConfidence ?? 0)
            .FirstOrDefault();

        if (strongestRule != null)
        {
            var machineLabel = string.IsNullOrWhiteSpace(strongestRule.CompatibilityMachineLabel)
                ? "seçili makine"
                : strongestRule.CompatibilityMachineLabel!.Trim();
            var pageText = string.IsNullOrWhiteSpace(strongestRule.PageNumber)
                ? string.Empty
                : $" Kaynak: Sf {strongestRule.PageNumber!.Trim()}.";
            var confidenceText = strongestRule.CompatibilityConfidence.HasValue
                ? $" Güven: %{strongestRule.CompatibilityConfidence.Value * 100:0}."
                : string.Empty;

            return strongestRule.CompatibilityLevel switch
            {
                "Exact" => $"Ustam, {code} kodlu {name} {machineLabel} için kesin uyumlu olarak kayıtlı.{confidenceText}{pageText}",
                "Likely" => $"Ustam, {code} kodlu {name} {machineLabel} için muhtemel aday olarak kayıtlı; takmadan önce eski parça kodu veya sayfa ile teyit et.{confidenceText}{pageText}",
                "SameAssembly" => $"Ustam, {code} kodlu {name} {machineLabel} ile aynı montaj grubunda görünüyor; bu tek başına kesin uyum anlamına gelmez.{confidenceText}{pageText}",
                "Incompatible" => $"Ustam, {code} kodlu {name} {machineLabel} için uyumsuz olarak işaretli. Farklı parça aramak gerekir.{confidenceText}{pageText}",
                _ => $"Ustam, {code} kodlu {name} için uyumluluk kaydı belirsiz. Modeli ve eski parça kodunu teyit edelim.{pageText}"
            };
        }

        var compatRows = products
            .Select(p =>
            {
                var brand = string.IsNullOrWhiteSpace(p.Brand) ? null : p.Brand.Trim();
                var model = string.IsNullOrWhiteSpace(p.Model) ? null : p.Model.Trim();
                if (brand is null && model is null)
                {
                    return null;
                }

                return brand is not null && model is not null
                    ? $"{brand} {model}"
                    : brand ?? model;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        var pageHints = products
            .Select(p => string.IsNullOrWhiteSpace(p.PageNumber) ? null : $"Sf {p.PageNumber!.Trim()}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (compatRows.Count > 0)
        {
            var compatText = string.Join(", ", compatRows);
            var pageText = pageHints.Count > 0 ? $" Kaynak: {string.Join(", ", pageHints)}." : string.Empty;
            return $"Ustam, {code} kodlu {name} katalogda şu model/makine bağlamlarında geçiyor: {compatText}.{pageText}";
        }

        if (pageHints.Count > 0)
        {
            return $"Ustam, {code} kodlu {name} bulundu ama model alanı bu kayıtlarda boş. Kaynak sayfalar: {string.Join(", ", pageHints)}.";
        }

        return $"Ustam, {code} kodlu {name} bulundu. Uyum için parça kartlarını ve katalog bağlamını aşağıda ekledim.";
    }

    private static int CompatibilityPriority(string? level)
    {
        return level switch
        {
            "Exact" => 0,
            "Likely" => 1,
            "SameAssembly" => 2,
            "Unknown" => 3,
            "Incompatible" => 4,
            _ => 5
        };
    }

    private static bool ShouldOverrideAiAnswer(string? aiAnswer)
    {
        if (string.IsNullOrWhiteSpace(aiAnswer))
        {
            return true;
        }

        var normalized = aiAnswer.Trim();
        var lowSignalFragments = new[]
        {
            "belirsizlik var",
            "doğru parçayı netleyelim",
            "katalog bulunmuyor",
            "tespit edilemedi",
            "sonuç bulamadım",
            "uygun parça bulamadım"
        };

        return lowSignalFragments.Any(fragment =>
            normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnavailableFeatureAnswer(string? aiAnswer)
    {
        if (string.IsNullOrWhiteSpace(aiAnswer))
        {
            return false;
        }

        var normalized = aiAnswer.Trim();
        return normalized.Contains("henüz aktif değil", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains("fiyat bilgisi", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("stok bilgisi", StringComparison.OrdinalIgnoreCase));
    }
}
