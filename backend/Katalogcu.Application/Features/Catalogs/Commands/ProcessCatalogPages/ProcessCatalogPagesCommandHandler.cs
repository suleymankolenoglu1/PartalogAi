using Katalogcu.Application.Common.Exceptions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Application.Features.Catalogs.Commands.ProcessCatalogPages;

public sealed class ProcessCatalogPagesCommandHandler : IRequestHandler<ProcessCatalogPagesCommand, OperationResult<ProcessCatalogPagesResponse>>
{
    private readonly ICatalogProcessingRepository _catalogProcessingRepository;
    private readonly ICatalogPageFileService _catalogPageFileService;
    private readonly ICatalogCoverMetadataService _catalogCoverMetadataService;
    private readonly IPartalogAiService _partalogAiService;
    private readonly IHotspotDetectionService _hotspotDetectionService;
    private readonly ILogger<ProcessCatalogPagesCommandHandler> _logger;

    public ProcessCatalogPagesCommandHandler(
        ICatalogProcessingRepository catalogProcessingRepository,
        ICatalogPageFileService catalogPageFileService,
        ICatalogCoverMetadataService catalogCoverMetadataService,
        IPartalogAiService partalogAiService,
        IHotspotDetectionService hotspotDetectionService,
        ILogger<ProcessCatalogPagesCommandHandler> logger)
    {
        _catalogProcessingRepository = catalogProcessingRepository;
        _catalogPageFileService = catalogPageFileService;
        _catalogCoverMetadataService = catalogCoverMetadataService;
        _partalogAiService = partalogAiService;
        _hotspotDetectionService = hotspotDetectionService;
        _logger = logger;
    }

    public async Task<OperationResult<ProcessCatalogPagesResponse>> Handle(ProcessCatalogPagesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Otonom işlem başladı: {CatalogId}", request.CatalogId);

        var catalog = await _catalogProcessingRepository.GetCatalogForProcessingAsync(request.CatalogId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<ProcessCatalogPagesResponse>.Failure("not_found", "Katalog bulunamadı.");
        }

        var pages = await _catalogProcessingRepository.GetCatalogPagesForProcessingAsync(request.CatalogId, cancellationToken);
        if (pages.Count == 0)
        {
            catalog.Status = "Error";
            catalog.UpdatedDate = DateTime.UtcNow;
            await _catalogProcessingRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<ProcessCatalogPagesResponse>.Failure("validation", "İşlenecek katalog sayfası bulunamadı.");
        }

        string? machineBrand = null;
        string? machineModel = null;
        var machineGroup = "General";
        string? catalogTitle = null;

        var processedPageCount = 0;
        var failedPageCount = 0;
        var savedItemCount = 0;
        var savedHotspotCount = 0;

        foreach (var page in pages)
        {
            _logger.LogInformation("🔄 Sayfa işleniyor: {CatalogId} | Sayfa {PageNumber}", request.CatalogId, page.PageNumber);

            try
            {
                var fileBytes = await _catalogPageFileService.ReadImageBytesAsync(page.ImageUrl, cancellationToken);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    failedPageCount++;
                    _logger.LogWarning("⚠️ Sayfa dosyası okunamadı: {CatalogId} | Sayfa {PageNumber}", request.CatalogId, page.PageNumber);
                    continue;
                }

                if (page.PageNumber == 1)
                {
                    var metadata = await _catalogCoverMetadataService.AnalyzeAsync(fileBytes, cancellationToken);
                    if (metadata != null)
                    {
                        machineModel = metadata.MachineModel;
                        machineBrand = metadata.MachineBrand;
                        machineGroup = string.IsNullOrWhiteSpace(metadata.MachineGroup) ? "General" : metadata.MachineGroup;
                        catalogTitle = metadata.CatalogTitle;

                        var newCatalogName = BuildCatalogName(machineModel, catalogTitle);
                        if (!string.IsNullOrWhiteSpace(newCatalogName))
                        {
                            catalog.Name = newCatalogName;
                        }
                    }
                }

                var analysis = await _partalogAiService.AnalyzePageAsync(fileBytes);
                page.AiDescription = analysis.Title ?? string.Empty;
                page.IsTechnicalDrawing = analysis.IsTechnicalDrawing;

                // Some parts-list pages are misclassified by the vision model.
                // We still don't want to force extraction on the catalog cover,
                // so only use the fallback for non-drawing pages after page 1.
                var shouldAttemptTableExtraction =
                    analysis.IsPartsList ||
                    (!analysis.IsTechnicalDrawing && page.PageNumber > 1);

                _logger.LogInformation(
                    "🧭 Sayfa AI kararı | Catalog={CatalogId} | Page={PageNumber} | Title={Title} | IsTechnicalDrawing={IsTechnicalDrawing} | IsPartsList={IsPartsList} | ShouldExtractTable={ShouldExtractTable}",
                    request.CatalogId,
                    page.PageNumber,
                    analysis.Title ?? string.Empty,
                    analysis.IsTechnicalDrawing,
                    analysis.IsPartsList,
                    shouldAttemptTableExtraction);

                if (shouldAttemptTableExtraction)
                {
                    var extractedItems = await _partalogAiService.ExtractTableAsync(
                        fileBytes,
                        page.PageNumber,
                        throwOnFailure: true);
                    await _catalogProcessingRepository.DeleteCatalogItemsByCatalogAndPageNumberAsync(
                        request.CatalogId,
                        page.PageNumber.ToString(),
                        cancellationToken);

                    if (extractedItems.Count > 0)
                    {
                        var catalogItems = new List<CatalogItem>(extractedItems.Count);
                        foreach (var item in extractedItems)
                        {
                            var catalogItem = BuildCatalogItem(
                                request.CatalogId,
                                page.PageNumber,
                                item,
                                machineBrand,
                                machineModel,
                                machineGroup,
                                analysis.Title);

                            catalogItems.Add(catalogItem);
                        }

                        await EnrichCatalogItemsWithCanonicalSearchTextAsync(catalogItems, cancellationToken);
                        await _catalogProcessingRepository.AddCatalogItemsAsync(catalogItems, cancellationToken);
                        savedItemCount += catalogItems.Count;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "📭 Table extraction ürün döndürmedi | Catalog={CatalogId} | Page={PageNumber}",
                            request.CatalogId,
                            page.PageNumber);
                    }
                }

                if (analysis.IsTechnicalDrawing)
                {
                    await _catalogProcessingRepository.DeleteHotspotsByPageIdAsync(page.Id, cancellationToken);

                    var hotspots = await _hotspotDetectionService.DetectHotspotsForPageAsync(
                        page.ImageUrl,
                        page.Id,
                        cancellationToken,
                        throwOnFailure: true);

                    if (hotspots.Count > 0)
                    {
                        await _catalogProcessingRepository.AddHotspotsAsync(hotspots, cancellationToken);
                        savedHotspotCount += hotspots.Count;
                    }
                }

                await _catalogProcessingRepository.SaveChangesAsync(cancellationToken);
                processedPageCount++;
            }
            catch (CatalogAiRetryableException ex)
            {
                failedPageCount++;
                _logger.LogWarning(
                    ex,
                    "♻️ Retryable OCR/YOLO hatası: {CatalogId} | Sayfa {PageNumber} | Operation={Operation}",
                    request.CatalogId,
                    page.PageNumber,
                    ex.Operation);
                throw;
            }
            catch (Exception ex)
            {
                failedPageCount++;
                _logger.LogError(ex, "❌ Sayfa işleme hatası: {CatalogId} | Sayfa {PageNumber}", request.CatalogId, page.PageNumber);
            }
        }

        if (processedPageCount == 0 || (savedItemCount == 0 && savedHotspotCount == 0))
        {
            catalog.Status = "Error";
            catalog.UpdatedDate = DateTime.UtcNow;
            await _catalogProcessingRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<ProcessCatalogPagesResponse>.Failure(
                "processing_failed",
                processedPageCount == 0
                    ? "Katalog sayfaları işlenemedi."
                    : "AI analizi tamamlandı ancak hiç parça veya hotspot üretilemedi.");
        }

        catalog.Status = "Published";
        catalog.UpdatedDate = DateTime.UtcNow;
        await _catalogProcessingRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await _partalogAiService.TriggerTrainingAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Eğitim tetiklenemedi: {CatalogId}", request.CatalogId);
        }

        _logger.LogInformation(
            "🏁 Katalog işlemi tamamlandı: {CatalogId} | Processed={Processed} Failed={Failed} Items={Items} Hotspots={Hotspots}",
            request.CatalogId,
            processedPageCount,
            failedPageCount,
            savedItemCount,
            savedHotspotCount);

        return OperationResult<ProcessCatalogPagesResponse>.Success(new ProcessCatalogPagesResponse
        {
            CatalogId = request.CatalogId,
            ProcessedPageCount = processedPageCount,
            FailedPageCount = failedPageCount,
            SavedItemCount = savedItemCount,
            SavedHotspotCount = savedHotspotCount
        });
    }

    private CatalogItem BuildCatalogItem(
        Guid catalogId,
        int pageNumber,
        ProductItemDto item,
        string? machineBrand,
        string? machineModel,
        string? machineGroup,
        string? mechanism)
    {
        return new CatalogItem
        {
            CatalogId = catalogId,
            PageNumber = pageNumber.ToString(),
            RefNumber = item.RefNumber,
            PartCode = item.PartCode ?? string.Empty,
            PartName = item.PartName ?? string.Empty,
            Description = item.Description ?? string.Empty,
            MachineBrand = machineBrand,
            MachineModel = machineModel,
            MachineGroup = machineGroup,
            Mechanism = mechanism,
            Dimensions = item.Dimensions
        };
    }

    private async Task EnrichCatalogItemsWithCanonicalSearchTextAsync(
        IReadOnlyList<CatalogItem> catalogItems,
        CancellationToken cancellationToken)
    {
        if (catalogItems.Count == 0)
        {
            return;
        }

        var searchTextRequests = catalogItems
            .Select(item => new IngestionSearchTextRequest(
                PartName: item.PartName,
                MachineBrandModel: BuildMachineBrandModel(item.MachineBrand, item.MachineModel),
                MachineBrand: item.MachineBrand,
                MachineModel: item.MachineModel,
                MachineGroup: item.MachineGroup,
                Category: item.MachineGroup,
                Description: item.Description,
                PartCode: item.PartCode,
                RefNo: item.RefNumber,
                Dimensions: item.Dimensions,
                Mechanism: item.Mechanism))
            .ToList();

        var searchTexts = await _partalogAiService.BuildSearchTextsAsync(searchTextRequests, cancellationToken);

        for (var i = 0; i < catalogItems.Count; i++)
        {
            var searchText = searchTexts[i];
            catalogItems[i].SearchText = searchText;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var vectorData = await _partalogAiService.GetEmbeddingAsync(searchText);
                if (vectorData is { Length: > 0 })
                {
                    catalogItems[i].Embedding = new(vectorData);
                }
            }
        }
    }

    private static string? BuildMachineBrandModel(string? machineBrand, string? machineModel)
    {
        var values = new[] { machineBrand, machineModel }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(" ", values);
    }

    private static string? BuildCatalogName(string? machineModel, string? catalogTitle)
    {
        if (string.IsNullOrWhiteSpace(machineModel))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(catalogTitle))
        {
            return machineModel.Trim();
        }

        return $"{machineModel.Trim()} ({catalogTitle.Trim()})";
    }
}
