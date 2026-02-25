using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.PublishCatalog;

public sealed record PublishCatalogCommand(Guid CatalogId, Guid UserId) : IRequest<OperationResult<PublishCatalogResponse>>;

public sealed class PublishCatalogResponse
{
    public string Message { get; init; } = "Katalog yayına alındı";
    public string Status { get; init; } = "Published";
}
