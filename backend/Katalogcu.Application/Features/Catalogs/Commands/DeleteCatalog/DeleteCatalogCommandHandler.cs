using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.DeleteCatalog;

public sealed class DeleteCatalogCommandHandler : IRequestHandler<DeleteCatalogCommand, OperationResult<bool>>
{
    private readonly ICatalogRepository _catalogRepository;

    public DeleteCatalogCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<bool>> Handle(DeleteCatalogCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<bool>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        var productIds = await _catalogRepository.GetProductIdsByCatalogIdAsync(request.CatalogId, cancellationToken);
        if (productIds.Count > 0)
        {
            await _catalogRepository.DeleteOrderItemsByProductIdsAsync(productIds, cancellationToken);
            await _catalogRepository.DeleteHotspotsByProductIdsAsync(productIds, cancellationToken);
        }

        await _catalogRepository.DeleteProductsByCatalogIdAsync(request.CatalogId, cancellationToken);
        await _catalogRepository.DeleteCatalogItemsByCatalogIdAsync(request.CatalogId, cancellationToken);
        await _catalogRepository.DeleteCatalogPagesByCatalogIdAsync(request.CatalogId, cancellationToken);
        _catalogRepository.RemoveCatalog(catalog);
        await _catalogRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}
