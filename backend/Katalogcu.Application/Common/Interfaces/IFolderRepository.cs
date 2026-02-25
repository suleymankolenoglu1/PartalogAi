using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IFolderRepository
{
    Task<IReadOnlyList<Folder>> GetFoldersByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Dictionary<Guid, int>> GetCatalogCountsByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> FolderNameExistsAsync(Guid userId, string name, CancellationToken cancellationToken);

    Task AddFolderAsync(Folder folder, CancellationToken cancellationToken);

    Task<Folder?> GetFolderByIdAsync(Guid folderId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Catalog>> GetCatalogsInFolderAsync(Guid folderId, CancellationToken cancellationToken);

    void RemoveFolder(Folder folder);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
