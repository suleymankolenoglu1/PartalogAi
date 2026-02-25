using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetOwnedProducts;

public sealed class GetOwnedProductsQueryHandler : IRequestHandler<GetOwnedProductsQuery, OperationResult<IReadOnlyList<ProductListItemDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public GetOwnedProductsQueryHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<IReadOnlyList<ProductListItemDto>>> Handle(GetOwnedProductsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<ProductListItemDto>>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var products = await _stockRepository.GetOwnedProductsForListAsync(_currentUser.UserId, cancellationToken);

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
