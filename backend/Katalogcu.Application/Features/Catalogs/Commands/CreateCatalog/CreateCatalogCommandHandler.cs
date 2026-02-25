using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.CreateCatalog;

public sealed class CreateCatalogCommandHandler : IRequestHandler<CreateCatalogCommand, OperationResult<Catalog>>
{
    private readonly ICatalogRepository _catalogRepository;

    public CreateCatalogCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<Catalog>> Handle(CreateCatalogCommand request, CancellationToken cancellationToken)
    {
        if (request.FolderId.HasValue)
        {
            var folderExists = await _catalogRepository.FolderExistsForUserAsync(request.FolderId.Value, request.UserId, cancellationToken);
            if (!folderExists)
            {
                return OperationResult<Catalog>.Failure("validation", "Hedef klasör bulunamadı veya size ait değil.");
            }
        }

        var catalog = new Catalog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            PdfUrl = request.PdfUrl?.Trim() ?? string.Empty,
            ImageUrl = request.ImageUrl?.Trim() ?? string.Empty,
            FolderId = request.FolderId,
            CreatedDate = DateTime.UtcNow,
            Status = "Uploading"
        };

        await _catalogRepository.AddCatalogAsync(catalog, cancellationToken);
        await _catalogRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<Catalog>.Success(catalog);
    }
}
