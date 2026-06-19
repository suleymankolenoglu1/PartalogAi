using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetOwnedProductsPage;

public sealed record GetOwnedProductsPageQuery(
    Guid? CatalogId = null,
    string? StockStatus = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 40)
    : IRequest<OperationResult<PagedOwnedProductsResponse>>;
