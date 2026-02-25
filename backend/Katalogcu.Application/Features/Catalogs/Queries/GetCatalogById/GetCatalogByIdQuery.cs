using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogById;

public sealed record GetCatalogByIdQuery(
    Guid CatalogId,
    Guid UserId,
    bool IsPublic,
    IReadOnlyCollection<Guid>? AllowedCatalogIds)
    : IRequest<OperationResult<Catalog>>;
