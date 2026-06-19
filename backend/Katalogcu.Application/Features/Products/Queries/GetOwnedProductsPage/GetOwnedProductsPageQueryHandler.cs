using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Products.Queries.Common;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetOwnedProductsPage;

public sealed class GetOwnedProductsPageQueryHandler
    : IRequestHandler<GetOwnedProductsPageQuery, OperationResult<PagedOwnedProductsResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public GetOwnedProductsPageQueryHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<PagedOwnedProductsResponse>> Handle(GetOwnedProductsPageQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<PagedOwnedProductsResponse>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var (items, totalCount) = await _stockRepository.GetOwnedProductsPageAsync(
            _currentUser.UserId,
            request.CatalogId,
            request.StockStatus,
            request.Search,
            skip,
            pageSize,
            cancellationToken);

        return OperationResult<PagedOwnedProductsResponse>.Success(new PagedOwnedProductsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }
}
