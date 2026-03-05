using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.UpdateHotspot;

public sealed class UpdateHotspotCommandHandler : IRequestHandler<UpdateHotspotCommand, OperationResult<Domain.Entities.Hotspot>>
{
    private readonly IHotspotRepository _hotspotRepository;

    public UpdateHotspotCommandHandler(IHotspotRepository hotspotRepository)
    {
        _hotspotRepository = hotspotRepository;
    }

    public async Task<OperationResult<Domain.Entities.Hotspot>> Handle(UpdateHotspotCommand request, CancellationToken cancellationToken)
    {
        var hotspot = await _hotspotRepository.GetHotspotByIdForUserAsync(request.HotspotId, request.UserId, cancellationToken);
        if (hotspot == null)
        {
            return OperationResult<Domain.Entities.Hotspot>.Failure("not_found", "Hotspot bulunamadı veya yetkiniz yok.");
        }

        hotspot.Left = request.Left;
        hotspot.Top = request.Top;
        hotspot.Width = request.Width;
        hotspot.Height = request.Height;
        hotspot.Label = string.IsNullOrWhiteSpace(request.Label) ? hotspot.Label : request.Label.Trim();
        hotspot.ProductId = request.ProductId;
        hotspot.UpdatedDate = DateTime.UtcNow;

        await _hotspotRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<Domain.Entities.Hotspot>.Success(hotspot);
    }
}
