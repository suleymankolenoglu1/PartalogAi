using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Commands.CreateFolder;

public sealed class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, OperationResult<CreateFolderResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFolderRepository _folderRepository;

    public CreateFolderCommandHandler(ICurrentUserService currentUser, IFolderRepository folderRepository)
    {
        _currentUser = currentUser;
        _folderRepository = folderRepository;
    }

    public async Task<OperationResult<CreateFolderResponse>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<CreateFolderResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var trimmedName = request.Name.Trim();
        var exists = await _folderRepository.FolderNameExistsAsync(_currentUser.UserId, trimmedName, cancellationToken);
        if (exists)
        {
            return OperationResult<CreateFolderResponse>.Failure("duplicate", "Bu isimde bir klasörünüz zaten var.");
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            UserId = _currentUser.UserId,
            CreatedDate = DateTime.UtcNow
        };

        await _folderRepository.AddFolderAsync(folder, cancellationToken);
        await _folderRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<CreateFolderResponse>.Success(new CreateFolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            UserId = folder.UserId,
            CreatedDate = folder.CreatedDate
        });
    }
}
