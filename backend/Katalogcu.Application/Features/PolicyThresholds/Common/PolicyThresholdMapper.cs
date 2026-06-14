using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public static class PolicyThresholdMapper
{
    public static PolicyThresholdDto ToDto(PolicyThreshold threshold)
    {
        return new PolicyThresholdDto
        {
            Id = threshold.Id,
            ScopeType = threshold.ScopeType,
            ScopeKey = threshold.ScopeKey,
            HighConfidence = threshold.HighConfidence,
            LowConfidence = threshold.LowConfidence,
            AmbiguityScoreDelta = threshold.AmbiguityScoreDelta,
            IsActive = threshold.IsActive,
            Version = threshold.Version,
            Notes = threshold.Notes,
            UpdatedBy = threshold.UpdatedBy,
            CreatedAt = threshold.CreatedDate,
            UpdatedAt = threshold.UpdatedDate
        };
    }

    public static object ToAuditShape(PolicyThreshold threshold)
    {
        return new
        {
            threshold.Id,
            threshold.ScopeType,
            threshold.ScopeKey,
            threshold.HighConfidence,
            threshold.LowConfidence,
            threshold.AmbiguityScoreDelta,
            threshold.IsActive,
            threshold.Version,
            threshold.Notes,
            threshold.UpdatedBy
        };
    }
}
