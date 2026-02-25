using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class FolderRepository : IFolderRepository
{
    private readonly AppDbContext _context;

    public FolderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Folder>> GetFoldersByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Folders
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetCatalogCountsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.FolderId != null)
            .GroupBy(c => c.FolderId!.Value)
            .Select(g => new { FolderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count, cancellationToken);
    }

    public Task<bool> FolderNameExistsAsync(Guid userId, string name, CancellationToken cancellationToken)
    {
        return _context.Folders.AnyAsync(f => f.UserId == userId && f.Name == name, cancellationToken);
    }

    public Task AddFolderAsync(Folder folder, CancellationToken cancellationToken)
    {
        return _context.Folders.AddAsync(folder, cancellationToken).AsTask();
    }

    public Task<Folder?> GetFolderByIdAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Catalog>> GetCatalogsInFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .Where(c => c.FolderId == folderId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveFolder(Folder folder)
    {
        _context.Folders.Remove(folder);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
