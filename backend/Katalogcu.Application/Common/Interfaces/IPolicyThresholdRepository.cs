using Katalogcu.Domain.Entities;
using Katalogcu.Application.Features.PolicyThresholds.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPolicyThresholdRepository
{
    Task<IReadOnlyList<PolicyThreshold>> GetPolicyThresholdsAsync(
        bool includeInactive,
        string? scopeType,
        Guid actorUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken);

    Task<PolicyThreshold?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PolicyThreshold?> GetActiveAsync(string scopeType, string scopeKey, CancellationToken cancellationToken);

    Task<bool> UserOwnsCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetOwnedCatalogScopeKeysAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PolicyThresholdOperationLog>> GetRecentPolicyOperationLogsAsync(
        int take,
        CancellationToken cancellationToken);

    void AddPolicyThreshold(PolicyThreshold threshold);

    void AddAuditLog(PlatformAuditLog auditLog);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
