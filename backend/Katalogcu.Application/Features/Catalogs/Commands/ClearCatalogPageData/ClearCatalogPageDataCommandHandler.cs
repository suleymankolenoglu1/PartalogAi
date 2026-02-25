using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.ClearCatalogPageData;

public sealed class ClearCatalogPageDataCommandHandler : IRequestHandler<ClearCatalogPageDataCommand, OperationResult<ClearCatalogPageDataResponse>>
{
    private readonly ICatalogRepository _catalogRepository;

    public ClearCatalogPageDataCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<ClearCatalogPageDataResponse>> Handle(ClearCatalogPageDataCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<ClearCatalogPageDataResponse>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        var page = await _catalogRepository.GetCatalogPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
        {
            return OperationResult<ClearCatalogPageDataResponse>.Failure("not_found", "Sayfa bulunamadı.");
        }

        await _catalogRepository.DeleteHotspotsByPageIdAsync(request.PageId, cancellationToken);
        await _catalogRepository.DeleteCatalogItemsByCatalogAndPageNumberAsync(
            request.CatalogId,
            page.PageNumber.ToString(),
            cancellationToken);

        return OperationResult<ClearCatalogPageDataResponse>.Success(new ClearCatalogPageDataResponse());
    }
}
