using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public static class PolicyRegressionCaseParser
{
    public static List<PolicyRegressionCaseDraft> ParseDrafts(string? jsonl)
    {
        if (string.IsNullOrWhiteSpace(jsonl))
        {
            return [];
        }

        var cases = new List<PolicyRegressionCaseDraft>();
        foreach (var rawLine in jsonl.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var hasText = TryGetJsonString(root, "text", out var text) && !string.IsNullOrWhiteSpace(text);
            var hasMessage = TryGetJsonString(root, "message", out var message) && !string.IsNullOrWhiteSpace(message);
            if (!hasText && !hasMessage)
            {
                continue;
            }

            TryGetJsonString(root, "id", out var id);
            var canonical = JsonSerializer.Serialize(root);
            cases.Add(new PolicyRegressionCaseDraft(id.Trim(), canonical, ToPolicyEvalCase(root)));
        }

        return cases;
    }

    public static string ComputeEvalCasesHash(IEnumerable<PolicyThresholdEvalCaseDto> cases)
    {
        var normalized = cases.Select(evalCase => new
        {
            id = PolicyThresholdRules.NormalizeOptional(evalCase.Id, 256),
            text = PolicyThresholdRules.NormalizeOptional(evalCase.Text ?? evalCase.Message, 4000),
            catalog_ids = NormalizeTerms(evalCase.CatalogIds).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            context_json = CanonicalizeJsonValue(evalCase.ContextJson),
            expected_codes = NormalizeTerms(evalCase.ExpectedCodes).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            required_terms = NormalizeTerms(evalCase.RequiredTerms).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            forbidden_terms = NormalizeTerms(evalCase.ForbiddenTerms).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            expect_no_codes = evalCase.ExpectNoCodes
        }).ToList();

        var json = JsonSerializer.Serialize(normalized);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static List<string> NormalizeTerms(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public static object? GetJsonProperty(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                        ? null
                        : property.Value.Clone();
                }
            }
        }

        return null;
    }

    public static List<string> GetJsonStringArray(JsonElement root, params string[] names)
    {
        var value = GetJsonProperty(root, names);
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
    }

    public static bool GetJsonBool(JsonElement root, params string[] names)
    {
        var value = GetJsonProperty(root, names);
        if (value is not JsonElement element)
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    public static bool TryGetJsonString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => string.Empty
            };
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static PolicyThresholdEvalCaseDto ToPolicyEvalCase(JsonElement root)
    {
        TryGetJsonString(root, "id", out var id);
        TryGetJsonString(root, "text", out var text);
        TryGetJsonString(root, "message", out var message);
        return new PolicyThresholdEvalCaseDto
        {
            Id = PolicyThresholdRules.NormalizeOptional(id, 256),
            Text = PolicyThresholdRules.NormalizeOptional(text, 4000),
            Message = PolicyThresholdRules.NormalizeOptional(message, 4000),
            CatalogIds = GetJsonStringArray(root, "catalog_ids", "catalogIds"),
            ContextJson = GetJsonProperty(root, "context_json", "contextJson", "context"),
            ExpectedCodes = GetJsonStringArray(root, "expected_codes", "expectedCodes"),
            RequiredTerms = GetJsonStringArray(root, "required_terms", "requiredTerms"),
            ForbiddenTerms = GetJsonStringArray(root, "forbidden_terms", "forbiddenTerms"),
            ExpectNoCodes = GetJsonBool(root, "expect_no_codes", "expectNoCodes")
        };
    }

    private static string? CanonicalizeJsonValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? null
                : JsonSerializer.Serialize(element);
        }

        return JsonSerializer.Serialize(value);
    }
}
