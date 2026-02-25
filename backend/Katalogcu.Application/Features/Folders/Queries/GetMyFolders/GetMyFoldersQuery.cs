using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Queries.GetMyFolders;

public sealed record GetMyFoldersQuery : IRequest<OperationResult<IReadOnlyList<FolderListItemDto>>>;

public sealed class FolderListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int CatalogCount { get; init; }
}
