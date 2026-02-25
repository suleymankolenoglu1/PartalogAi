using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Commands.DeleteFolder;

public sealed class DeleteFolderCommandHandler : IRequestHandler<DeleteFolderCommand, OperationResult<DeleteFolderResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFolderRepository _folderRepository;

    public DeleteFolderCommandHandler(ICurrentUserService currentUser, IFolderRepository folderRepository)
    {
        _currentUser = currentUser;
        _folderRepository = folderRepository;
    }

    public async Task<OperationResult<DeleteFolderResponse>> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<DeleteFolderResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var folder = await _folderRepository.GetFolderByIdAsync(request.FolderId, _currentUser.UserId, cancellationToken);
        if (folder == null)
        {
            return OperationResult<DeleteFolderResponse>.Failure("not_found", "Klasör bulunamadı veya silme yetkiniz yok.");
        }

        var catalogsInFolder = await _folderRepository.GetCatalogsInFolderAsync(request.FolderId, cancellationToken);
        foreach (var catalog in catalogsInFolder)
        {
            catalog.FolderId = null;
        }

        _folderRepository.RemoveFolder(folder);
        await _folderRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<DeleteFolderResponse>.Success(new DeleteFolderResponse());
    }
}
