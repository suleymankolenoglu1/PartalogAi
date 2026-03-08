using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.DetectHotspots;

public sealed class DetectHotspotsCommandHandler : IRequestHandler<DetectHotspotsCommand, OperationResult<DetectHotspotsResponse>>
{
    private readonly IHotspotRepository _hotspotRepository;
    private readonly IHotspotDetectionService _hotspotDetectionService;

    public DetectHotspotsCommandHandler(
        IHotspotRepository hotspotRepository,
        IHotspotDetectionService hotspotDetectionService)
    {
        _hotspotRepository = hotspotRepository;
        _hotspotDetectionService = hotspotDetectionService;
    }

    public async Task<OperationResult<DetectHotspotsResponse>> Handle(DetectHotspotsCommand request, CancellationToken cancellationToken)
    {
        var page = await _hotspotRepository.GetCatalogPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
        {
            return OperationResult<DetectHotspotsResponse>.Failure("not_found", "Sayfa bulunamadı");
        }

        if (string.IsNullOrWhiteSpace(page.ImageUrl))
        {
            return OperationResult<DetectHotspotsResponse>.Failure("validation", "Sayfanın görüntüsü yok");
        }

        IReadOnlyList<Domain.Entities.Hotspot> detectedHotspots;
        try
        {
            detectedHotspots = await _hotspotDetectionService.DetectHotspotsForPageAsync(
                page.ImageUrl,
                page.Id,
                cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            return OperationResult<DetectHotspotsResponse>.Failure("validation", ex.Message);
        }

        if (detectedHotspots.Count == 0)
        {
            var existingAiHotspots = await _hotspotRepository.GetHotspotsByPageIdAsync(page.Id, cancellationToken);
            var aiHotspotsToRemove = existingAiHotspots.Where(h => h.IsAiDetected).ToList();
            if (aiHotspotsToRemove.Count > 0)
            {
                _hotspotRepository.RemoveHotspots(aiHotspotsToRemove);
                await _hotspotRepository.SaveChangesAsync(cancellationToken);
            }

            return OperationResult<DetectHotspotsResponse>.Success(new DetectHotspotsResponse
            {
                PageId = page.Id,
                DetectedCount = 0,
                Message = "Hiç hotspot tespit edilemedi",
                Hotspots = []
            });
        }

        var existingHotspots = await _hotspotRepository.GetHotspotsByPageIdAsync(page.Id, cancellationToken);
        var existingAiDetected = existingHotspots.Where(h => h.IsAiDetected).ToList();
        if (existingAiDetected.Count > 0)
        {
            _hotspotRepository.RemoveHotspots(existingAiDetected);
        }

        await _hotspotRepository.AddHotspotsAsync(detectedHotspots, cancellationToken);
        await _hotspotRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<DetectHotspotsResponse>.Success(new DetectHotspotsResponse
        {
            PageId = page.Id,
            DetectedCount = detectedHotspots.Count,
            Message = $"{detectedHotspots.Count} hotspot tespit edildi ve kaydedildi",
            Hotspots = detectedHotspots
        });
    }
}
