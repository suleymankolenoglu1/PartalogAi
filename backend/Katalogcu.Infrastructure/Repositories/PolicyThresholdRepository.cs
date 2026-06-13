using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class PolicyThresholdRepository : IPolicyThresholdRepository
{
    private readonly AppDbContext _context;

    public PolicyThresholdRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PolicyThreshold>> GetPolicyThresholdsAsync(
        bool includeInactive,
        string? scopeType,
        Guid actorUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken)
    {
        var query = _context.PolicyThresholds.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            query = query.Where(x => x.ScopeType == scopeType);
        }

        if (!isPlatformAdmin)
        {
            var ownedCatalogIds = await _context.Catalogs
                .AsNoTracking()
                .Where(c => c.UserId == actorUserId)
                .Select(c => c.Id.ToString())
                .ToListAsync(cancellationToken);

            query = query.Where(x => x.ScopeType == PolicyThreshold.CatalogScope && ownedCatalogIds.Contains(x.ScopeKey));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<PolicyThreshold?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.PolicyThresholds.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<PolicyThreshold?> GetActiveAsync(string scopeType, string scopeKey, CancellationToken cancellationToken)
    {
        return _context.PolicyThresholds
            .Where(x => x.IsActive && x.ScopeType == scopeType && x.ScopeKey == scopeKey)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> UserOwnsCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs
            .AsNoTracking()
            .AnyAsync(c => c.Id == catalogId && c.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetOwnedCatalogScopeKeysAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Id.ToString())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyThresholdOperationLog>> GetRecentPolicyOperationLogsAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await _context.PlatformAuditLogs
            .AsNoTracking()
            .Where(x => x.Action.StartsWith("PolicyThreshold."))
            .OrderByDescending(x => x.CreatedDate)
            .Take(take)
            .Select(x => new PolicyThresholdOperationLog
            {
                Id = x.Id,
                Action = x.Action,
                ActorEmail = x.ActorEmail,
                ActorRole = x.ActorRole,
                CreatedDate = x.CreatedDate,
                Payload = x.Payload
            })
            .ToListAsync(cancellationToken);
    }

    public void AddPolicyThreshold(PolicyThreshold threshold)
    {
        _context.PolicyThresholds.Add(threshold);
    }

    public void AddAuditLog(PlatformAuditLog auditLog)
    {
        _context.PlatformAuditLogs.Add(auditLog);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
