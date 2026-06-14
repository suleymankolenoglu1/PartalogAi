using System.Text.Json;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public static class PolicyThresholdOperationParser
{
    public static bool TryParseJsonObject(string? payload, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static PolicyAuditScope ExtractAuditScope(JsonElement payload)
    {
        var scopeType = ExtractString(payload, "scopeType")
                        ?? ExtractString(payload, "after", "policy", "ScopeType")
                        ?? ExtractString(payload, "after", "policy", "scopeType")
                        ?? ExtractString(payload, "after", "active", "ScopeType")
                        ?? ExtractString(payload, "after", "active", "scopeType")
                        ?? ExtractString(payload, "before", "ScopeType")
                        ?? ExtractString(payload, "before", "scopeType")
                        ?? ExtractString(payload, "before", "target", "ScopeType")
                        ?? ExtractString(payload, "before", "target", "scopeType")
                        ?? ExtractString(payload, "before", "active", "ScopeType")
                        ?? ExtractString(payload, "before", "active", "scopeType");
        var scopeKey = ExtractString(payload, "after", "policy", "ScopeKey")
                       ?? ExtractString(payload, "after", "policy", "scopeKey")
                       ?? ExtractString(payload, "after", "active", "ScopeKey")
                       ?? ExtractString(payload, "after", "active", "scopeKey")
                       ?? ExtractString(payload, "before", "ScopeKey")
                       ?? ExtractString(payload, "before", "scopeKey")
                       ?? ExtractString(payload, "before", "target", "ScopeKey")
                       ?? ExtractString(payload, "before", "target", "scopeKey")
                       ?? ExtractString(payload, "before", "active", "ScopeKey")
                       ?? ExtractString(payload, "before", "active", "scopeKey");
        return new PolicyAuditScope(scopeType, scopeKey);
    }

    public static string MapPolicyOperationTitle(string action)
    {
        return action switch
        {
            "PolicyThreshold.Created" => "Policy oluşturuldu",
            "PolicyThreshold.Updated" => "Policy güncellendi",
            "PolicyThreshold.Deactivated" => "Policy pasifleştirildi",
            "PolicyThreshold.Activated" => "Policy rollback/aktivasyon",
            "PolicyThreshold.RegressionCasesPromoted" => "Regression case promote edildi",
            _ => action
        };
    }

    public static string BuildPolicyScopeLabel(PolicyAuditScope scope)
    {
        if (string.Equals(scope.ScopeType, PolicyThreshold.GlobalScope, StringComparison.OrdinalIgnoreCase))
        {
            return "Global";
        }

        if (string.Equals(scope.ScopeType, PolicyThreshold.BrandScope, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(scope.ScopeKey) ? "Marka" : $"Marka: {scope.ScopeKey}";
        }

        if (string.Equals(scope.ScopeType, PolicyThreshold.CatalogScope, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(scope.ScopeKey) ? "Katalog" : $"Katalog: {scope.ScopeKey}";
        }

        return "Policy";
    }

    public static string? ExtractString(JsonElement root, params string[] path)
    {
        if (!TryGetPath(root, out var current, path))
        {
            return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => PolicyThresholdRules.NormalizeOptional(current.GetString(), 512),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static int? ExtractInt(JsonElement root, params string[] path)
    {
        if (!TryGetPath(root, out var current, path))
        {
            return null;
        }

        if (current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var number))
        {
            return number;
        }

        if (current.ValueKind == JsonValueKind.String && int.TryParse(current.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryGetPath(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var found = false;
            foreach (var property in value.EnumerateObject())
            {
                if (!string.Equals(property.Name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = property.Value;
                found = true;
                break;
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
