using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.DeleteCatalogItem;

public sealed class DeleteCatalogItemCommandHandler : IRequestHandler<DeleteCatalogItemCommand, OperationResult<bool>>
{
    private readonly ICatalogRepository _catalogRepository;

    public DeleteCatalogItemCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<bool>> Handle(DeleteCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _catalogRepository.GetCatalogItemByIdForUserAsync(request.CatalogItemId, request.UserId, cancellationToken);
        if (item == null)
        {
            return OperationResult<bool>.Failure("not_found", "Parça satırı bulunamadı veya yetkiniz yok.");
        }

        _catalogRepository.RemoveCatalogItem(item);
        await _catalogRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}
