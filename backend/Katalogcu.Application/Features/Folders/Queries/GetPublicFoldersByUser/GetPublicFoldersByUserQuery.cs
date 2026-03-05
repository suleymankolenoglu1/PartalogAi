using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Folders.Queries.GetPublicFoldersByUser;

public sealed record GetPublicFoldersByUserQuery(Guid UserId, IReadOnlyCollection<Guid>? AllowedCatalogIds)
    : IRequest<OperationResult<IReadOnlyList<PublicFolderListItemDto>>>;

public sealed class PublicFolderListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}

