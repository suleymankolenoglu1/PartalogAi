using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class ManualImportFileRepository : IManualImportFileRepository
{
    private readonly AppDbContext _context;

    public ManualImportFileRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(ManualImportFile manualImportFile, CancellationToken cancellationToken)
    {
        return _context.ManualImportFiles.AddAsync(manualImportFile, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<ManualImportFile>> GetHistoryBySiteAsync(Guid externalSiteId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.ManualImportFiles
            .AsNoTracking()
            .Where(x => x.ExternalSiteId == externalSiteId
                        && x.ExternalSite != null
                        && x.ExternalSite.UserId == userId)
            .OrderByDescending(x => x.ImportedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
