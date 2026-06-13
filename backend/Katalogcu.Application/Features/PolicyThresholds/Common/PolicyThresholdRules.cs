using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public static class PolicyThresholdRules
{
    private static readonly HashSet<string> ValidScopeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        PolicyThreshold.GlobalScope,
        PolicyThreshold.BrandScope,
        PolicyThreshold.CatalogScope
    };

    public static OperationResult<(string ScopeType, string ScopeKey)> ValidateAndNormalize(PolicyThresholdRequestDto request)
    {
        var scopeType = NormalizeScopeType(request.ScopeType) ?? string.Empty;
        var scopeKey = NormalizeScopeKey(scopeType, request.ScopeKey);

        if (string.IsNullOrWhiteSpace(scopeType))
        {
            return OperationResult<(string, string)>.Failure("validation", "ScopeType Global, Brand veya Catalog olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return OperationResult<(string, string)>.Failure("validation", "ScopeKey zorunludur.");
        }

        if (request.HighConfidence is null && request.LowConfidence is null && request.AmbiguityScoreDelta is null)
        {
            return OperationResult<(string, string)>.Failure("validation", "En az bir threshold değeri girilmelidir.");
        }

        if (!IsThresholdValue(request.HighConfidence) ||
            !IsThresholdValue(request.LowConfidence) ||
            !IsThresholdValue(request.AmbiguityScoreDelta))
        {
            return OperationResult<(string, string)>.Failure("validation", "Threshold değerleri 0 ile 1 arasında olmalıdır.");
        }

        if (request.LowConfidence.HasValue &&
            request.HighConfidence.HasValue &&
            request.LowConfidence.Value > request.HighConfidence.Value)
        {
            return OperationResult<(string, string)>.Failure("validation", "LowConfidence, HighConfidence değerinden büyük olamaz.");
        }

        return OperationResult<(string, string)>.Success((scopeType, scopeKey));
    }

    public static string? NormalizeScopeType(string? scopeType)
    {
        var value = (scopeType ?? string.Empty).Trim();
        if (!ValidScopeTypes.Contains(value))
        {
            return null;
        }

        if (value.Equals(PolicyThreshold.GlobalScope, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyThreshold.GlobalScope;
        }

        if (value.Equals(PolicyThreshold.BrandScope, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyThreshold.BrandScope;
        }

        return PolicyThreshold.CatalogScope;
    }

    public static string NormalizeScopeKey(string scopeType, string? scopeKey)
    {
        if (scopeType == PolicyThreshold.GlobalScope)
        {
            return "default";
        }

        var value = (scopeKey ?? string.Empty).Trim();
        return scopeType == PolicyThreshold.BrandScope ? value.ToLowerInvariant() : value;
    }

    public static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    public static bool IsThresholdValue(decimal? value)
    {
        return value is null or (>= 0m and <= 1m);
    }
}
