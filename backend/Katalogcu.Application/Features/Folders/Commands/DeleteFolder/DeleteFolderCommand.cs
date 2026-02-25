using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Commands.DeleteFolder;

public sealed record DeleteFolderCommand(Guid FolderId) : IRequest<OperationResult<DeleteFolderResponse>>;

public sealed class DeleteFolderResponse
{
    public string Message { get; init; } = "Klasör başarıyla silindi.";
}
