using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICompatibilityRepository
{
    Task<IReadOnlyList<MachineModel>> GetMachineModelsAsync(CancellationToken cancellationToken);

    Task<MachineModel?> GetMachineModelAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CatalogItemExistsAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartCompatibilityRule>> GetRulesForCatalogItemIdsAsync(
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken cancellationToken);

    Task AddMachineModelAsync(MachineModel machineModel, CancellationToken cancellationToken);

    Task AddRuleAsync(PartCompatibilityRule rule, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
