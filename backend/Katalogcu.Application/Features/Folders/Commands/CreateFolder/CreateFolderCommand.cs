using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Commands.CreateFolder;

public sealed record CreateFolderCommand(string Name) : IRequest<OperationResult<CreateFolderResponse>>;

public sealed class CreateFolderResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public DateTime CreatedDate { get; init; }
}
