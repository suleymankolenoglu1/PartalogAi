using Katalogcu.Application.Features.PolicyThresholds.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPolicyThresholdAuditWriter
{
    void AddAuditLog(
        PolicyThresholdActor actor,
        string action,
        string? scopeType,
        object? before,
        object? after);
}
