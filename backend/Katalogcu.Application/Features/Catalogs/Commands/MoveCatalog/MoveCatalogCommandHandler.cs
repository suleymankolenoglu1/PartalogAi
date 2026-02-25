using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.MoveCatalog;

public sealed class MoveCatalogCommandHandler : IRequestHandler<MoveCatalogCommand, OperationResult<MoveCatalogResponse>>
{
    private readonly ICatalogRepository _catalogRepository;

    public MoveCatalogCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<MoveCatalogResponse>> Handle(MoveCatalogCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<MoveCatalogResponse>.Failure("not_found", "Katalog bulunamadı.");
        }

        if (request.FolderId.HasValue)
        {
            var folderExists = await _catalogRepository.FolderExistsForUserAsync(request.FolderId.Value, request.UserId, cancellationToken);
            if (!folderExists)
            {
                return OperationResult<MoveCatalogResponse>.Failure("validation", "Hedef klasör bulunamadı veya size ait değil.");
            }
        }

        catalog.FolderId = request.FolderId;
        catalog.UpdatedDate = DateTime.UtcNow;
        await _catalogRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<MoveCatalogResponse>.Success(new MoveCatalogResponse
        {
            FolderId = catalog.FolderId
        });
    }
}
