using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.CatalogItems.Common;
using Katalogcu.Application.Features.Catalogs.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.CreateCatalogItem;

public sealed class CreateCatalogItemCommandHandler : IRequestHandler<CreateCatalogItemCommand, OperationResult<CatalogPageItemDto>>
{
    private readonly ICatalogRepository _catalogRepository;

    public CreateCatalogItemCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<CatalogPageItemDto>> Handle(CreateCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<CatalogPageItemDto>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        var pageExists = await _catalogRepository.CatalogPageExistsForCatalogAsync(
            request.CatalogId,
            request.PageNumber,
            request.UserId,
            cancellationToken);

        if (!pageExists)
        {
            return OperationResult<CatalogPageItemDto>.Failure("validation", "Seçilen sayfa katalogda bulunamadı.");
        }

        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            CatalogId = request.CatalogId,
            PageNumber = request.PageNumber.ToString(),
            RefNumber = request.RefNo.Trim(),
            PartCode = request.PartCode.Trim(),
            PartName = request.PartName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            VisualPageNumber = request.PageNumber,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _catalogRepository.AddCatalogItemAsync(item, cancellationToken);
        await _catalogRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<CatalogPageItemDto>.Success(CatalogItemMapper.ToDto(item));
    }
}
