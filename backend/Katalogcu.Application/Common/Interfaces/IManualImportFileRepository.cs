using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IManualImportFileRepository
{
    Task AddAsync(ManualImportFile manualImportFile, CancellationToken cancellationToken);
    Task<IReadOnlyList<ManualImportFile>> GetHistoryBySiteAsync(Guid externalSiteId, Guid userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
