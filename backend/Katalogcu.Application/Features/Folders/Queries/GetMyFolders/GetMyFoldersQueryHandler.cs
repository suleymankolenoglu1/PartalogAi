using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Queries.GetMyFolders;

public sealed class GetMyFoldersQueryHandler : IRequestHandler<GetMyFoldersQuery, OperationResult<IReadOnlyList<FolderListItemDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFolderRepository _folderRepository;

    public GetMyFoldersQueryHandler(ICurrentUserService currentUser, IFolderRepository folderRepository)
    {
        _currentUser = currentUser;
        _folderRepository = folderRepository;
    }

    public async Task<OperationResult<IReadOnlyList<FolderListItemDto>>> Handle(GetMyFoldersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<FolderListItemDto>>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var folders = await _folderRepository.GetFoldersByUserAsync(_currentUser.UserId, cancellationToken);
        var counts = await _folderRepository.GetCatalogCountsByUserAsync(_currentUser.UserId, cancellationToken);

        var result = folders.Select(f => new FolderListItemDto
        {
            Id = f.Id,
            Name = f.Name,
            CatalogCount = counts.TryGetValue(f.Id, out var count) ? count : 0
        }).ToList();

        return OperationResult<IReadOnlyList<FolderListItemDto>>.Success(result);
    }
}
