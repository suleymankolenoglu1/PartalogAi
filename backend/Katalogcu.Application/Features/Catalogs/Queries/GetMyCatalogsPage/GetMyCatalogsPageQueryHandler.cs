using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogsPage;

public sealed class GetMyCatalogsPageQueryHandler : IRequestHandler<GetMyCatalogsPageQuery, OperationResult<PagedCatalogListResponse>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetMyCatalogsPageQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<PagedCatalogListResponse>> Handle(GetMyCatalogsPageQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var (items, totalCount) = await _catalogRepository.GetCatalogListPageByUserAsync(
            request.UserId,
            request.FolderId,
            skip,
            pageSize,
            request.Search,
            cancellationToken);

        return OperationResult<PagedCatalogListResponse>.Success(new PagedCatalogListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }
}
