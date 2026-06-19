using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CompatibilityRepository : ICompatibilityRepository
{
    private readonly AppDbContext _context;

    public CompatibilityRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MachineModel>> GetMachineModelsAsync(CancellationToken cancellationToken)
    {
        return await _context.MachineModels
            .AsNoTracking()
            .OrderBy(m => m.Brand)
            .ThenBy(m => m.Model)
            .ThenBy(m => m.Variant)
            .ToListAsync(cancellationToken);
    }

    public Task<MachineModel?> GetMachineModelAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.MachineModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<bool> CatalogItemExistsAsync(Guid catalogItemId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.AnyAsync(item => item.Id == catalogItemId, cancellationToken);
    }

    public async Task<IReadOnlyList<PartCompatibilityRule>> GetRulesForCatalogItemIdsAsync(
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken cancellationToken)
    {
        if (catalogItemIds.Count == 0)
        {
            return [];
        }

        return await _context.PartCompatibilityRules
            .AsNoTracking()
            .Include(r => r.MachineModel)
            .Where(r => catalogItemIds.Contains(r.CatalogItemId))
            .ToListAsync(cancellationToken);
    }

    public Task AddMachineModelAsync(MachineModel machineModel, CancellationToken cancellationToken)
    {
        return _context.MachineModels.AddAsync(machineModel, cancellationToken).AsTask();
    }

    public Task AddRuleAsync(PartCompatibilityRule rule, CancellationToken cancellationToken)
    {
        return _context.PartCompatibilityRules.AddAsync(rule, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
