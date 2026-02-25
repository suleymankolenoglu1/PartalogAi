using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.FailCatalogUpload;

public sealed class FailCatalogUploadCommandHandler : IRequestHandler<FailCatalogUploadCommand, OperationResult<FailCatalogUploadResponse>>
{
    private readonly ICatalogRepository _catalogRepository;

    public FailCatalogUploadCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<FailCatalogUploadResponse>> Handle(FailCatalogUploadCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<FailCatalogUploadResponse>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        catalog.Status = "Error";
        catalog.UpdatedDate = DateTime.UtcNow;
        await _catalogRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<FailCatalogUploadResponse>.Success(new FailCatalogUploadResponse
        {
            CatalogId = catalog.Id,
            Status = catalog.Status
        });
    }
}
