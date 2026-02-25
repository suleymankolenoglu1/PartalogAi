using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.MoveCatalog;

public sealed record MoveCatalogCommand(Guid CatalogId, Guid UserId, Guid? FolderId)
    : IRequest<OperationResult<MoveCatalogResponse>>;

public sealed class MoveCatalogResponse
{
    public string Message { get; init; } = "Katalog başarıyla taşındı.";
    public Guid? FolderId { get; init; }
}
