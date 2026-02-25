using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetCatalogProducts;

public sealed record GetCatalogProductsQuery(
    Guid UserId,
    Guid CatalogId,
    bool PublishedOnly,
    IReadOnlyCollection<Guid>? AllowedCatalogIds)
    : IRequest<OperationResult<IReadOnlyList<ProductListItemDto>>>;
