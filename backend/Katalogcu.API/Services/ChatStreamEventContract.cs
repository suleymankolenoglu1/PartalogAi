using System.Text.Json;
using System.Text.Json.Serialization;

namespace Katalogcu.API.Services;

public static class ChatStreamEventContract
{
    public const int SchemaVersion = 1;
    public const string CompletionStatus = "completed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public sealed record ChatStreamFallback(
        [property: JsonPropertyName("used")] bool Used,
        [property: JsonPropertyName("reason")] string? Reason = null);

    public sealed record ChatStreamCompletion(
        [property: JsonPropertyName("status")] string Status);

    public sealed record ChatStreamEvent(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("fallback")] ChatStreamFallback Fallback,
        [property: JsonPropertyName("sources")] JsonElement? Sources = null,
        [property: JsonPropertyName("token")] string? Token = null,
        [property: JsonPropertyName("debugIntent")] JsonElement? DebugIntent = null,
        [property: JsonPropertyName("searchTrace")] JsonElement? SearchTrace = null,
        [property: JsonPropertyName("completion")] ChatStreamCompletion? Completion = null);

    public static ChatStreamEvent CreateSources(
        object? sources,
        object? debugIntent = null,
        object? searchTrace = null,
        bool fallbackUsed = false,
        string? fallbackReason = null)
    {
        return new ChatStreamEvent(
            SchemaVersion,
            "sources",
            new ChatStreamFallback(fallbackUsed, fallbackReason),
            Sources: ToJsonElement(sources ?? Array.Empty<object>()),
            DebugIntent: debugIntent is null ? null : ToJsonElement(debugIntent),
            SearchTrace: searchTrace is null ? null : ToJsonElement(searchTrace));
    }

    public static ChatStreamEvent CreateToken(
        string token,
        bool fallbackUsed = false,
        string? fallbackReason = null)
    {
        return new ChatStreamEvent(
            SchemaVersion,
            "token",
            new ChatStreamFallback(fallbackUsed, fallbackReason),
            Token: token);
    }

    public static ChatStreamEvent CreateDone(
        bool fallbackUsed = false,
        string? fallbackReason = null,
        string status = CompletionStatus)
    {
        return new ChatStreamEvent(
            SchemaVersion,
            "done",
            new ChatStreamFallback(fallbackUsed, fallbackReason),
            Completion: new ChatStreamCompletion(status));
    }

    public static string ToSseDataLine(ChatStreamEvent streamEvent)
    {
        Validate(streamEvent);
        return $"data: {JsonSerializer.Serialize(streamEvent, JsonOptions)}\n\n";
    }

    public static bool TryExtractDataPayload(string rawLine, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        var trimmed = rawLine.Trim();
        if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
        {
            return false;
        }

        payload = trimmed[5..].Trim();
        return payload.Length > 0;
    }

    public static string CombineDataPayloads(IEnumerable<string> rawLines)
    {
        var payloads = rawLines
            .Select(line => TryExtractDataPayload(line, out var payload) ? payload : null)
            .Where(payload => !string.IsNullOrWhiteSpace(payload))
            .Cast<string>()
            .ToList();

        return string.Join("\n", payloads);
    }

    public static bool TryParseDataPayload(
        string payload,
        out ChatStreamEvent? streamEvent,
        out string? error)
    {
        streamEvent = null;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("schemaVersion", out var schemaVersionElement) ||
                schemaVersionElement.ValueKind != JsonValueKind.Number ||
                schemaVersionElement.GetInt32() != SchemaVersion)
            {
                error = $"Unsupported stream schemaVersion in payload: {payload}";
                return false;
            }

            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                error = "Missing stream type.";
                return false;
            }

            var type = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(type))
            {
                error = "Stream type cannot be empty.";
                return false;
            }

            if (!root.TryGetProperty("fallback", out var fallbackElement) ||
                fallbackElement.ValueKind != JsonValueKind.Object ||
                !fallbackElement.TryGetProperty("used", out var fallbackUsedElement) ||
                (fallbackUsedElement.ValueKind != JsonValueKind.True && fallbackUsedElement.ValueKind != JsonValueKind.False))
            {
                error = "Missing fallback contract.";
                return false;
            }

            string? fallbackReason = null;
            if (fallbackElement.TryGetProperty("reason", out var fallbackReasonElement))
            {
                if (fallbackReasonElement.ValueKind == JsonValueKind.String)
                {
                    fallbackReason = fallbackReasonElement.GetString();
                }
                else if (fallbackReasonElement.ValueKind != JsonValueKind.Null)
                {
                    error = "fallback.reason must be string or null.";
                    return false;
                }
            }

            var fallback = new ChatStreamFallback(fallbackUsedElement.GetBoolean(), fallbackReason);

            switch (type)
            {
                case "sources":
                    if (!root.TryGetProperty("sources", out var sourcesElement) ||
                        sourcesElement.ValueKind != JsonValueKind.Array)
                    {
                        error = "sources event must include an array.";
                        return false;
                    }

                    JsonElement? debugIntent = null;
                    if (root.TryGetProperty("debugIntent", out var debugIntentElement))
                    {
                        debugIntent = debugIntentElement.Clone();
                    }

                    JsonElement? searchTrace = null;
                    if (root.TryGetProperty("searchTrace", out var searchTraceElement))
                    {
                        searchTrace = searchTraceElement.Clone();
                    }

                    streamEvent = new ChatStreamEvent(
                        SchemaVersion,
                        "sources",
                        fallback,
                        Sources: sourcesElement.Clone(),
                        DebugIntent: debugIntent,
                        SearchTrace: searchTrace);
                    return true;

                case "token":
                    if (!root.TryGetProperty("token", out var tokenElement) ||
                        tokenElement.ValueKind != JsonValueKind.String)
                    {
                        error = "token event must include token text.";
                        return false;
                    }

                    var token = tokenElement.GetString();
                    if (string.IsNullOrEmpty(token))
                    {
                        error = "token event cannot have empty token text.";
                        return false;
                    }

                    streamEvent = new ChatStreamEvent(
                        SchemaVersion,
                        "token",
                        fallback,
                        Token: token);
                    return true;

                case "done":
                    if (!root.TryGetProperty("completion", out var completionElement) ||
                        completionElement.ValueKind != JsonValueKind.Object ||
                        !completionElement.TryGetProperty("status", out var statusElement) ||
                        statusElement.ValueKind != JsonValueKind.String)
                    {
                        error = "done event must include completion.status.";
                        return false;
                    }

                    var status = statusElement.GetString();
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        error = "completion.status cannot be empty.";
                        return false;
                    }

                    streamEvent = new ChatStreamEvent(
                        SchemaVersion,
                        "done",
                        fallback,
                        Completion: new ChatStreamCompletion(status));
                    return true;

                default:
                    error = $"Unsupported stream event type '{type}'.";
                    return false;
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void Validate(ChatStreamEvent streamEvent)
    {
        if (streamEvent.SchemaVersion != SchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported stream schemaVersion '{streamEvent.SchemaVersion}'.");
        }

        if (streamEvent.Fallback is null)
        {
            throw new InvalidOperationException("Stream event fallback contract is required.");
        }

        switch (streamEvent.Type)
        {
            case "sources":
                if (streamEvent.Sources is null || streamEvent.Sources.Value.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("sources event must include an array.");
                }
                break;

            case "token":
                if (string.IsNullOrEmpty(streamEvent.Token))
                {
                    throw new InvalidOperationException("token event must include token text.");
                }
                break;

            case "done":
                if (streamEvent.Completion is null || string.IsNullOrWhiteSpace(streamEvent.Completion.Status))
                {
                    throw new InvalidOperationException("done event must include completion.status.");
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported stream event type '{streamEvent.Type}'.");
        }
    }

    private static JsonElement ToJsonElement(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        return document.RootElement.Clone();
    }
}
