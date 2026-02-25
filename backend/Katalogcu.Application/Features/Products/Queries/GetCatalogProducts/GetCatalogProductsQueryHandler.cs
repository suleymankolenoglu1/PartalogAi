using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetCatalogProducts;

public sealed class GetCatalogProductsQueryHandler : IRequestHandler<GetCatalogProductsQuery, OperationResult<IReadOnlyList<ProductListItemDto>>>
{
    private readonly IStockRepository _stockRepository;

    public GetCatalogProductsQueryHandler(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<IReadOnlyList<ProductListItemDto>>> Handle(GetCatalogProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _stockRepository.GetCatalogProductsForListAsync(
            request.UserId,
            request.CatalogId,
            request.PublishedOnly,
            request.AllowedCatalogIds,
            cancellationToken);

        var result = products.Select(p => new ProductListItemDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            OemNo = p.OemNo,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl,
            Category = p.Category,
            CatalogName = p.Catalog?.Name ?? "Genel Stok",
            CatalogId = p.CatalogId
        }).ToList();

        return OperationResult<IReadOnlyList<ProductListItemDto>>.Success(result);
    }
}
