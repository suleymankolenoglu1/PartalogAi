using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.PublishCatalog;

public sealed class PublishCatalogCommandHandler : IRequestHandler<PublishCatalogCommand, OperationResult<PublishCatalogResponse>>
{
    private readonly ICatalogRepository _catalogRepository;

    public PublishCatalogCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<PublishCatalogResponse>> Handle(PublishCatalogCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<PublishCatalogResponse>.Failure("not_found", "Katalog bulunamadı.");
        }

        catalog.Status = "Published";
        catalog.UpdatedDate = DateTime.UtcNow;
        await _catalogRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<PublishCatalogResponse>.Success(new PublishCatalogResponse
        {
            Status = catalog.Status
        });
    }
}
