using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetOwnedProducts;

public sealed record GetOwnedProductsQuery : IRequest<OperationResult<IReadOnlyList<ProductListItemDto>>>;
