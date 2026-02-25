using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.CompleteCatalogUpload;

public sealed class CompleteCatalogUploadCommandHandler : IRequestHandler<CompleteCatalogUploadCommand, OperationResult<CompleteCatalogUploadResponse>>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogPdfPageService _catalogPdfPageService;

    public CompleteCatalogUploadCommandHandler(
        ICatalogRepository catalogRepository,
        ICatalogPdfPageService catalogPdfPageService)
    {
        _catalogRepository = catalogRepository;
        _catalogPdfPageService = catalogPdfPageService;
    }

    public async Task<OperationResult<CompleteCatalogUploadResponse>> Handle(CompleteCatalogUploadCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<CompleteCatalogUploadResponse>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        if (string.IsNullOrWhiteSpace(catalog.PdfUrl))
        {
            return OperationResult<CompleteCatalogUploadResponse>.Failure("validation", "PDF bilgisi bulunamadı.");
        }

        var pageImageUrls = await _catalogPdfPageService.ConvertCatalogPdfToPageImageUrlsAsync(catalog.PdfUrl, cancellationToken);
        if (pageImageUrls.Count == 0)
        {
            return OperationResult<CompleteCatalogUploadResponse>.Failure("validation", "PDF sayfaları üretilemedi.");
        }

        var pages = pageImageUrls
            .Select((imageUrl, index) => new CatalogPage
            {
                CatalogId = catalog.Id,
                PageNumber = index + 1,
                ImageUrl = imageUrl
            })
            .ToList();

        await _catalogRepository.AddCatalogPagesAsync(pages, cancellationToken);

        catalog.Status = "ReadyToProcess";
        catalog.UpdatedDate = DateTime.UtcNow;
        await _catalogRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<CompleteCatalogUploadResponse>.Success(new CompleteCatalogUploadResponse
        {
            CatalogId = catalog.Id,
            Status = catalog.Status,
            PageCount = pages.Count
        });
    }
}
