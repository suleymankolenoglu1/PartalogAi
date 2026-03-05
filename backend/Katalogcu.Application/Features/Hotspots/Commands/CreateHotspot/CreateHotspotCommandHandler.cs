using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;

public sealed class CreateHotspotCommandHandler : IRequestHandler<CreateHotspotCommand, OperationResult<Hotspot>>
{
    private readonly IHotspotRepository _hotspotRepository;

    public CreateHotspotCommandHandler(IHotspotRepository hotspotRepository)
    {
        _hotspotRepository = hotspotRepository;
    }

    public async Task<OperationResult<Hotspot>> Handle(CreateHotspotCommand request, CancellationToken cancellationToken)
    {
        var page = await _hotspotRepository.GetCatalogPageByIdForUserAsync(request.PageId, request.UserId, cancellationToken);
        if (page == null)
        {
            return OperationResult<Hotspot>.Failure("not_found", "Sayfa bulunamadı veya yetkiniz yok.");
        }

        var hotspot = new Hotspot
        {
            Id = Guid.NewGuid(),
            PageId = request.PageId,
            Left = request.Left,
            Top = request.Top,
            Width = request.Width <= 0 ? 3.0 : request.Width,
            Height = request.Height <= 0 ? 2.0 : request.Height,
            Label = string.IsNullOrWhiteSpace(request.Label) ? "?" : request.Label,
            IsAiDetected = request.IsAiDetected,
            AiConfidence = request.AiConfidence,
            ProductId = request.ProductId,
            CreatedDate = DateTime.UtcNow
        };

        await _hotspotRepository.AddHotspotAsync(hotspot, cancellationToken);
        await _hotspotRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<Hotspot>.Success(hotspot);
    }
}
