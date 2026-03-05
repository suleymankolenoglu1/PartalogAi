using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogPageItems;

public sealed record GetCatalogPageItemsQuery(
    Guid CatalogId,
    int PageNumber,
    Guid UserId,
    bool IsPublic,
    IReadOnlyCollection<Guid>? AllowedCatalogIds,
    bool StrictPage = false)
    : IRequest<OperationResult<IReadOnlyList<CatalogPageItemDto>>>;
