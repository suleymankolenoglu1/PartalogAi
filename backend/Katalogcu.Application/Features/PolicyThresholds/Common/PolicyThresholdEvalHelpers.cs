using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public static class PolicyThresholdEvalHelpers
{
    public static string BuildPolicyOverrideJson(PolicyThresholdRequestDto request, string scopeType, string scopeKey)
    {
        var payload = new Dictionary<string, object?>
        {
            ["source"] = $"candidate:{scopeType.ToLowerInvariant()}:{scopeKey}"
        };
        if (request.HighConfidence.HasValue)
        {
            payload["high_confidence"] = decimal.ToDouble(request.HighConfidence.Value);
        }
        if (request.LowConfidence.HasValue)
        {
            payload["low_confidence"] = decimal.ToDouble(request.LowConfidence.Value);
        }
        if (request.AmbiguityScoreDelta.HasValue)
        {
            payload["ambiguity_score_delta"] = decimal.ToDouble(request.AmbiguityScoreDelta.Value);
        }

        return JsonSerializer.Serialize(payload);
    }

    public static List<string> ResolveEvalCatalogIds(PolicyThresholdEvalCaseDto evalCase, string scopeType, string scopeKey)
    {
        var catalogIds = (evalCase.CatalogIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (catalogIds.Count == 0 && string.Equals(scopeType, PolicyThreshold.CatalogScope, StringComparison.OrdinalIgnoreCase))
        {
            catalogIds.Add(scopeKey);
        }

        return catalogIds;
    }

    public static string? SerializeContext(object? context)
    {
        if (context is null)
        {
            return null;
        }

        if (context is JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetRawText();
        }

        return JsonSerializer.Serialize(context);
    }

    public static List<string> ExtractCodes(string value)
    {
        return Regex.Matches(value ?? string.Empty, @"\b[A-Z0-9][A-Z0-9\-]{4,}\b", RegexOptions.IgnoreCase)
            .Select(x => x.Value.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public static bool ContainsInvariant(string text, string term)
    {
        return (text ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public static string TrimPreview(string value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return text.Length <= 220 ? text : text[..220] + "...";
    }
}
