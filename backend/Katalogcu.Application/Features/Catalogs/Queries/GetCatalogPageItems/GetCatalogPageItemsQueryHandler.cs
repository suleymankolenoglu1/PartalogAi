using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogPageItems;

public sealed class GetCatalogPageItemsQueryHandler : IRequestHandler<GetCatalogPageItemsQuery, OperationResult<IReadOnlyList<CatalogPageItemDto>>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetCatalogPageItemsQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<IReadOnlyList<CatalogPageItemDto>>> Handle(GetCatalogPageItemsQuery request, CancellationToken cancellationToken)
    {
        async Task<IReadOnlyList<Domain.Entities.CatalogItem>> FetchForPageAsync(int page)
        {
            return await _catalogRepository.GetCatalogItemsForPageAsync(
                request.CatalogId,
                page.ToString(),
                request.UserId,
                request.IsPublic,
                request.AllowedCatalogIds,
                cancellationToken);
        }

        var catalogItems = await FetchForPageAsync(request.PageNumber);
        if (!catalogItems.Any()) catalogItems = await FetchForPageAsync(request.PageNumber + 1);
        if (!catalogItems.Any() && request.PageNumber > 1) catalogItems = await FetchForPageAsync(request.PageNumber - 1);

        if (!catalogItems.Any())
        {
            return OperationResult<IReadOnlyList<CatalogPageItemDto>>.Success([]);
        }

        var itemCodes = catalogItems.Select(ci => ci.PartCode).Distinct().ToList();
        var stockedProducts = await _catalogRepository.GetOwnedStockedProductsByCodesAsync(
            request.UserId,
            itemCodes,
            cancellationToken);

        var result = catalogItems.Select(item =>
        {
            var isStocked = stockedProducts.ContainsKey(item.PartCode);
            var product = isStocked ? stockedProducts[item.PartCode] : null;

            return new CatalogPageItemDto
            {
                CatalogItemId = item.Id,
                RefNo = item.RefNumber,
                PartCode = item.PartCode,
                PartName = item.PartName,
                Description = item.Description,
                IsStocked = isStocked,
                ProductId = product?.Id,
                Price = product?.Price,
                LocalName = product?.Name
            };
        }).ToList();

        return OperationResult<IReadOnlyList<CatalogPageItemDto>>.Success(result);
    }
}
