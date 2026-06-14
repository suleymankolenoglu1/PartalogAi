using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Chat.Common;

namespace Katalogcu.API.Services;

public interface IChatStreamProxyService
{
    Task<ChatStreamProxyResult> ProxyAskStreamAsync(
        HttpResponse response,
        string? text,
        string? history,
        string? contextJson,
        IReadOnlyCollection<string> catalogIds,
        IFormFile? image,
        string? userPlan,
        int? aiLimitPerMonth,
        int? aiUsedThisMonth,
        CancellationToken cancellationToken);
}

public sealed record ChatStreamProxyResult(bool Billable, string? FallbackReason = null)
{
    public static ChatStreamProxyResult NotBillable(string? fallbackReason = null) => new(false, fallbackReason);
}

public sealed class ChatStreamProxyService : IChatStreamProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IChatQueryService _chatQueryService;
    private readonly ILogger<ChatStreamProxyService> _logger;

    public ChatStreamProxyService(
        IHttpClientFactory httpClientFactory,
        IChatQueryService chatQueryService,
        ILogger<ChatStreamProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _chatQueryService = chatQueryService;
        _logger = logger;
    }

    public async Task<ChatStreamProxyResult> ProxyAskStreamAsync(
        HttpResponse response,
        string? text,
        string? history,
        string? contextJson,
        IReadOnlyCollection<string> catalogIds,
        IFormFile? image,
        string? userPlan,
        int? aiLimitPerMonth,
        int? aiUsedThisMonth,
        CancellationToken cancellationToken)
    {
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";

        var httpClient = _httpClientFactory.CreateClient("PartalogAi");

        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StringContent(text ?? string.Empty), "text");
        formContent.Add(new StringContent(history ?? "[]"), "history");
        if (!string.IsNullOrWhiteSpace(contextJson))
        {
            formContent.Add(new StringContent(contextJson), "context_json");
        }
        formContent.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(catalogIds)), "catalog_ids");
        if (!string.IsNullOrWhiteSpace(userPlan))
        {
            formContent.Add(new StringContent(userPlan), "user_plan");
        }
        if (aiLimitPerMonth.HasValue)
        {
            formContent.Add(new StringContent(aiLimitPerMonth.Value.ToString()), "ai_limit_per_month");
        }
        if (aiUsedThisMonth.HasValue)
        {
            formContent.Add(new StringContent(aiUsedThisMonth.Value.ToString()), "ai_used_this_month");
        }

        if (image != null)
        {
            var imageContent = new StreamContent(image.OpenReadStream());
            formContent.Add(imageContent, "file", image.FileName);
        }

        try
        {
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream") { Content = formContent };
            using var pythonResponse = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!pythonResponse.IsSuccessStatusCode)
            {
                var errorBody = await pythonResponse.Content.ReadAsStringAsync(cancellationToken);
                var (message, reason) = TryReadAiError(errorBody);
                var fallbackReason = pythonResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? FirstNonEmpty(reason, "ai_capacity_limited")
                    : FirstNonEmpty(reason, "ai_upstream_error");

                _logger.LogWarning(
                    "AskStream upstream başarısız döndü | Status={StatusCode} | Reason={Reason} | Body={Body}",
                    (int)pythonResponse.StatusCode,
                    fallbackReason,
                    errorBody);

                await WriteFallbackStreamAsync(
                    response,
                    FirstNonEmpty(message, "AI servisine şu an ulaşılamıyor. Lütfen daha sonra tekrar deneyin."),
                    fallbackReason,
                    cancellationToken);

                return ChatStreamProxyResult.NotBillable(fallbackReason);
            }

            using var stream = await pythonResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var catalogGuids = ParseCatalogGuids(catalogIds);
            var billing = new StreamBillingState();

            var eventLines = new List<string>();
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    await FlushEventAsync(eventLines, response, catalogGuids, billing, cancellationToken);
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    await FlushEventAsync(eventLines, response, catalogGuids, billing, cancellationToken);
                    continue;
                }

                eventLines.Add(line);
            }

            return billing.ToResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected or request aborted.
            return ChatStreamProxyResult.NotBillable("client_disconnected");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "AskStream upstream zaman aşımına uğradı");
            await WriteFallbackStreamAsync(
                response,
                "AI yanıtı zaman aşımına uğradı. Lütfen tekrar deneyin.",
                "ai_timeout",
                cancellationToken);
            return ChatStreamProxyResult.NotBillable("ai_timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AskStream upstream bağlantı hatası");
            await WriteFallbackStreamAsync(
                response,
                "AI servisine şu an ulaşılamıyor. Lütfen daha sonra tekrar deneyin.",
                "ai_upstream_error",
                cancellationToken);
            return ChatStreamProxyResult.NotBillable("ai_upstream_error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskStream proxy hatası");
            throw;
        }
    }

    private static async Task WriteFallbackStreamAsync(
        HttpResponse response,
        string message,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync(
            ChatStreamEventContract.ToSseDataLine(
                ChatStreamEventContract.CreateSources(
                    Array.Empty<object>(),
                    fallbackUsed: true,
                    fallbackReason: fallbackReason)),
            cancellationToken);
        await response.WriteAsync(
            ChatStreamEventContract.ToSseDataLine(
                ChatStreamEventContract.CreateToken(
                    message,
                    fallbackUsed: true,
                    fallbackReason: fallbackReason)),
            cancellationToken);
        await response.WriteAsync(
            ChatStreamEventContract.ToSseDataLine(
                ChatStreamEventContract.CreateDone(
                    fallbackUsed: true,
                    fallbackReason: fallbackReason)),
            cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static (string? Message, string? Reason) TryReadAiError(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.String)
                {
                    return (detail.GetString(), null);
                }

                if (detail.ValueKind == JsonValueKind.Object)
                {
                    return (
                        GetString(detail, "message"),
                        GetString(detail, "reason"));
                }
            }

            return (
                GetString(root, "message"),
                GetString(root, "reason"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private async Task FlushEventAsync(
        List<string> eventLines,
        HttpResponse response,
        IReadOnlyCollection<Guid> catalogIds,
        StreamBillingState billing,
        CancellationToken cancellationToken)
    {
        if (eventLines.Count == 0)
        {
            return;
        }

        var payload = ChatStreamEventContract.CombineDataPayloads(eventLines);
        eventLines.Clear();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        if (!ChatStreamEventContract.TryParseDataPayload(payload, out var streamEvent, out var error) || streamEvent is null)
        {
            throw new InvalidDataException(error ?? "Invalid stream event payload.");
        }

        if (streamEvent.Type == "sources")
        {
            LogSearchTrace(streamEvent.SearchTrace);
            streamEvent = await EnrichSourcesEventAsync(streamEvent, catalogIds, cancellationToken);
        }

        billing.Observe(streamEvent);

        await response.WriteAsync(ChatStreamEventContract.ToSseDataLine(streamEvent), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private async Task<ChatStreamEventContract.ChatStreamEvent> EnrichSourcesEventAsync(
        ChatStreamEventContract.ChatStreamEvent streamEvent,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken)
    {
        var effectiveCatalogIds = ResolveEffectiveCatalogIds(catalogIds, streamEvent.DebugIntent);
        if (effectiveCatalogIds.Count == 0 ||
            streamEvent.Sources is null ||
            streamEvent.Sources.Value.ValueKind != JsonValueKind.Array)
        {
            return streamEvent;
        }

        var sourceElements = streamEvent.Sources.Value.EnumerateArray().Select(e => e.Clone()).ToList();
        var sourceInputs = sourceElements
            .Select(source => ToChatSourceInput(source, streamEvent.DebugIntent))
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .ToList();

        if (sourceInputs.Count == 0)
        {
            return streamEvent;
        }

        var enrichedParts = await _chatQueryService.EnrichPythonSourcesAsync(sourceInputs, effectiveCatalogIds, cancellationToken);
        if (enrichedParts.Count == 0)
        {
            return streamEvent;
        }

        var sourceMetaByCode = sourceElements
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .Select(e => new { Code = GetString(e, "code", "Code"), Element = e })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => x.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Element, StringComparer.OrdinalIgnoreCase);

        var payload = enrichedParts.Select(part =>
        {
            sourceMetaByCode.TryGetValue(part.Code, out var sourceMeta);
            return new
            {
                id = part.Id,
                catalogItemId = part.Id,
                code = part.Code,
                refNo = part.RefNumber,
                name = part.Name,
                brand = part.Brand,
                description = part.Description,
                catalogId = part.CatalogId,
                pageNumber = part.PageNumber,
                model = part.Model,
                price = part.Price,
                stockStatus = part.StockStatus,
                imageUrl = part.ImageUrl,
                quantity = part.Quantity,
                query = part.SourceQuery ?? GetString(sourceMeta, "query", "Query"),
                similarity = part.SourceSimilarity ?? GetDouble(sourceMeta, "similarity", "Similarity"),
                matchReason = ResolveMatchReason(part, sourceMeta),
                confidenceLabel = ResolveConfidenceLabel(part, sourceMeta),
                requiresVerification = ResolveRequiresVerification(part, sourceMeta),
                visualMatch = GetBool(sourceMeta, "visualMatch", "visual_match"),
                visualImageUrl = GetString(sourceMeta, "visualImageUrl", "visual_image_url"),
                visualSimilarity = GetDouble(sourceMeta, "visualSimilarity", "visual_similarity"),
                fallback = part.Fallback ?? GetBool(sourceMeta, "fallback"),
                fallbackReason = part.FallbackReason ?? GetString(sourceMeta, "fallbackReason", "fallback_reason"),
                compatibilityLevel = part.CompatibilityLevel,
                compatibilitySourceType = part.CompatibilitySourceType,
                compatibilityConfidence = part.CompatibilityConfidence,
                compatibilityMachineLabel = part.CompatibilityMachineLabel,
                compatibilityNotes = part.CompatibilityNotes,
            };
        }).ToList();

        return ChatStreamEventContract.CreateSources(
            payload,
            streamEvent.DebugIntent,
            streamEvent.SearchTrace,
            streamEvent.Fallback.Used,
            streamEvent.Fallback.Reason);
    }

    private void LogSearchTrace(JsonElement? searchTrace)
    {
        if (!searchTrace.HasValue || searchTrace.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var trace = searchTrace.Value;
        var originalQuery = GetString(trace, "original_query", "originalQuery");
        var retrievedCount = GetInt(trace, "retrieved_candidates_count", "retrievedCandidatesCount");
        var filteredCount = GetInt(trace, "compatibility_gate_filtered_count", "compatibilityGateFilteredCount");
        string? decision = null;
        if (trace.TryGetProperty("final_decision", out var finalDecision) ||
            trace.TryGetProperty("finalDecision", out finalDecision))
        {
            decision = GetString(finalDecision, "decision");
        }

        _logger.LogInformation(
            "Chat search trace | Query={OriginalQuery} | Retrieved={RetrievedCount} | CompatibilityFiltered={CompatibilityFilteredCount} | Decision={Decision}",
            originalQuery,
            retrievedCount,
            filteredCount,
            decision);
    }

    private static IReadOnlyCollection<Guid> ParseCatalogGuids(IReadOnlyCollection<string> catalogIds)
    {
        return catalogIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyCollection<Guid> ResolveEffectiveCatalogIds(
        IReadOnlyCollection<Guid> requestedCatalogIds,
        JsonElement? debugIntent)
    {
        var resolvedCatalogIds = GetGuidArray(debugIntent, "resolved_catalog_ids", "resolvedCatalogIds");
        return resolvedCatalogIds.Count > 0 ? resolvedCatalogIds : requestedCatalogIds;
    }

    private static ChatSourceInput ToChatSourceInput(JsonElement source, JsonElement? debugIntent)
    {
        return new ChatSourceInput
        {
            Code = GetString(source, "code", "Code"),
            Name = GetString(source, "name", "Name"),
            Model = GetString(source, "machine_model", "machineModel"),
            LegacyModel = GetString(source, "model", "Model"),
            Description = GetString(source, "description", "Description"),
            LegacyDescription = GetString(source, "desc", "legacyDescription"),
            Query = GetString(source, "query", "Query"),
            Similarity = GetDouble(source, "similarity", "Similarity"),
            MatchReason = GetString(source, "matchReason", "match_reason"),
            ConfidenceLabel = GetString(source, "confidenceLabel", "confidence_label"),
            RequiresVerification = GetBool(source, "requiresVerification", "requires_verification"),
            Fallback = GetBool(source, "fallback"),
            FallbackReason = GetString(source, "fallbackReason", "fallback_reason"),
            CatalogId = GetGuid(source, "catalogId", "catalog_id", "CatalogId"),
            PageNumber = GetString(source, "pageNumber", "page_number", "PageNumber"),
            RequestedMachineBrand = debugIntent.HasValue ? GetString(debugIntent.Value, "brand") : null,
            RequestedMachineModel = debugIntent.HasValue ? GetString(debugIntent.Value, "machine_model", "machineModel") : null,
            RequestedMachineVariant = debugIntent.HasValue ? GetString(debugIntent.Value, "machine_variant", "machineVariant") : null
        };
    }

    private static string? GetString(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static Guid? GetGuid(JsonElement source, params string[] names)
    {
        var value = GetString(source, names);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static int? GetInt(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static IReadOnlyCollection<Guid> GetGuidArray(JsonElement? source, params string[] names)
    {
        if (!source.HasValue || source.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<Guid>();
        }

        foreach (var name in names)
        {
            if (!source.Value.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return value
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .Distinct()
                .ToList();
        }

        return Array.Empty<Guid>();
    }

    private static string ResolveMatchReason(EnrichedPartDto part, JsonElement sourceMeta)
    {
        var explicitReason = part.MatchReason ?? GetString(sourceMeta, "matchReason", "match_reason");
        if (!string.IsNullOrWhiteSpace(explicitReason))
        {
            return explicitReason;
        }

        var reasons = new List<string>();
        var fallbackReason = part.FallbackReason ?? GetString(sourceMeta, "fallbackReason", "fallback_reason");
        if ((part.Fallback ?? GetBool(sourceMeta, "fallback")) == true)
        {
            reasons.Add(FormatFallbackReasonForMatch(fallbackReason));
        }

        var score = part.SourceSimilarity ?? GetDouble(sourceMeta, "similarity", "Similarity");
        if (score.HasValue)
        {
            if (score.Value >= 0.99)
            {
                reasons.Add("kod/kayıt eşleşmesi güçlü görünüyor");
            }
            else if (score.Value >= 0.72 && !string.IsNullOrWhiteSpace(CleanQueryLabel(part.SourceQuery ?? GetString(sourceMeta, "query", "Query"))))
            {
                reasons.Add($"{CleanQueryLabel(part.SourceQuery ?? GetString(sourceMeta, "query", "Query"))} araması bu parçayla eşleşti");
            }
            else if (score.Value >= 0.72)
            {
                reasons.Add("katalogdaki parça adıyla güçlü eşleşti");
            }
            else if (score.Value >= 0.60)
            {
                reasons.Add("katalogda aday parça olarak öne çıktı");
            }
        }

        var queryLabel = CleanQueryLabel(part.SourceQuery ?? GetString(sourceMeta, "query", "Query"));
        if (!string.IsNullOrWhiteSpace(queryLabel) && !reasons.Any(reason => reason.Contains(queryLabel, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add($"{queryLabel} aramasından aday oldu");
        }

        if (!string.IsNullOrWhiteSpace(part.PageNumber) && !string.IsNullOrWhiteSpace(part.RefNumber))
        {
            reasons.Add($"katalogda Sf {part.PageNumber}, Ref {part.RefNumber} olarak görünüyor");
        }
        else if (!string.IsNullOrWhiteSpace(part.PageNumber))
        {
            reasons.Add($"katalogda Sf {part.PageNumber} üzerinde görünüyor");
        }

        return reasons.Count > 0
            ? string.Join("; ", reasons.Distinct().Take(3))
            : "katalog kaydında aday olarak bulundu";
    }

    private static string ResolveConfidenceLabel(EnrichedPartDto part, JsonElement sourceMeta)
    {
        var explicitLabel = part.ConfidenceLabel ?? GetString(sourceMeta, "confidenceLabel", "confidence_label");
        if (!string.IsNullOrWhiteSpace(explicitLabel))
        {
            return explicitLabel;
        }

        if ((part.Fallback ?? GetBool(sourceMeta, "fallback")) == true)
        {
            return "Teyit gerekli";
        }

        var score = part.SourceSimilarity ?? GetDouble(sourceMeta, "similarity", "Similarity");
        if (score.HasValue)
        {
            if (score.Value >= 0.99)
            {
                return "Yüksek güven";
            }

            if (score.Value >= 0.72)
            {
                return "Yüksek aday";
            }

            if (score.Value >= 0.60)
            {
                return "Orta aday";
            }
        }

        return "Aday";
    }

    private static bool ResolveRequiresVerification(EnrichedPartDto part, JsonElement sourceMeta)
    {
        var explicitValue = part.RequiresVerification ?? GetBool(sourceMeta, "requiresVerification", "requires_verification");
        if (explicitValue.HasValue)
        {
            return explicitValue.Value;
        }

        if ((part.Fallback ?? GetBool(sourceMeta, "fallback")) == true)
        {
            return true;
        }

        var score = part.SourceSimilarity ?? GetDouble(sourceMeta, "similarity", "Similarity");
        return !score.HasValue || score.Value < 0.72;
    }

    private static string FormatFallbackReasonForMatch(string? reason)
    {
        return reason switch
        {
            "context_page_match" => "aktif sayfa/ref bağlamından aday oldu",
            "brand_removed" => "marka filtresi kaldırılınca aday oldu; uyum teyidi gerekir",
            "machine_group_removed" => "makine tipi filtresi kaldırılınca aday oldu; uyum teyidi gerekir",
            "all_filters_removed" => "filtreler gevşetilince aday oldu; mutlaka model/kod teyidi gerekir",
            _ => "genişletilmiş aramada aday oldu"
        };
    }

    private static string? CleanQueryLabel(string? query)
    {
        var value = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Split('|')[0].Trim();
        return value.Length > 42 ? $"{value[..39].TrimEnd()}..." : value;
    }

    private static double? GetDouble(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private sealed class StreamBillingState
    {
        private bool _sawToken;
        private bool _sawDone;
        private bool _fallbackUsed;
        private string? _fallbackReason;

        public void Observe(ChatStreamEventContract.ChatStreamEvent streamEvent)
        {
            if (streamEvent.Fallback.Used)
            {
                _fallbackUsed = true;
                _fallbackReason ??= streamEvent.Fallback.Reason;
            }

            if (streamEvent.Type == "token")
            {
                _sawToken = true;
            }

            if (streamEvent.Type == "done")
            {
                _sawDone = true;
            }
        }

        public ChatStreamProxyResult ToResult()
        {
            return new ChatStreamProxyResult(
                Billable: _sawDone && _sawToken && !_fallbackUsed,
                FallbackReason: _fallbackReason);
        }
    }
}
