using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.CatalogItems.Common;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.UpdateCatalogItem;

public sealed class UpdateCatalogItemCommandHandler : IRequestHandler<UpdateCatalogItemCommand, OperationResult<CatalogPageItemDto>>
{
    private readonly ICatalogRepository _catalogRepository;

    public UpdateCatalogItemCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<CatalogPageItemDto>> Handle(UpdateCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _catalogRepository.GetCatalogItemByIdForUserAsync(request.CatalogItemId, request.UserId, cancellationToken);
        if (item == null)
        {
            return OperationResult<CatalogPageItemDto>.Failure("not_found", "Parça satırı bulunamadı veya yetkiniz yok.");
        }

        item.RefNumber = request.RefNo.Trim();
        item.PartCode = request.PartCode.Trim();
        item.PartName = request.PartName.Trim();
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.UpdatedDate = DateTime.UtcNow;

        await _catalogRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<CatalogPageItemDto>.Success(CatalogItemMapper.ToDto(item));
    }
}
