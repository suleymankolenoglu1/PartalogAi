using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Queries.GetPublicFoldersByUser;

public sealed class GetPublicFoldersByUserQueryHandler
    : IRequestHandler<GetPublicFoldersByUserQuery, OperationResult<IReadOnlyList<PublicFolderListItemDto>>>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IFolderRepository _folderRepository;

    public GetPublicFoldersByUserQueryHandler(ICatalogRepository catalogRepository, IFolderRepository folderRepository)
    {
        _catalogRepository = catalogRepository;
        _folderRepository = folderRepository;
    }

    public async Task<OperationResult<IReadOnlyList<PublicFolderListItemDto>>> Handle(
        GetPublicFoldersByUserQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<PublicFolderListItemDto>>.Failure("validation", "UserId zorunludur.");
        }

        var publicCatalogs = await _catalogRepository.GetPublicCatalogsByUserAsync(
            request.UserId,
            request.AllowedCatalogIds,
            cancellationToken);

        var folderCounts = publicCatalogs
            .Where(c => c.FolderId.HasValue)
            .GroupBy(c => c.FolderId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var folders = await _folderRepository.GetFoldersByUserAsync(request.UserId, cancellationToken);

        var result = folders
            .OrderBy(f => f.Name)
            .Select(f => new PublicFolderListItemDto
            {
                Id = f.Id,
                Name = f.Name,
                ItemCount = folderCounts.TryGetValue(f.Id, out var count) ? count : 0
            })
            .ToList();

        return OperationResult<IReadOnlyList<PublicFolderListItemDto>>.Success(result);
    }
}
