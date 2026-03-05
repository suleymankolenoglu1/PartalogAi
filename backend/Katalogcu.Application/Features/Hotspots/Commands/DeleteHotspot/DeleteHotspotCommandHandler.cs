using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.DeleteHotspot;

public sealed class DeleteHotspotCommandHandler : IRequestHandler<DeleteHotspotCommand, OperationResult<bool>>
{
    private readonly IHotspotRepository _hotspotRepository;

    public DeleteHotspotCommandHandler(IHotspotRepository hotspotRepository)
    {
        _hotspotRepository = hotspotRepository;
    }

    public async Task<OperationResult<bool>> Handle(DeleteHotspotCommand request, CancellationToken cancellationToken)
    {
        var hotspot = await _hotspotRepository.GetHotspotByIdForUserAsync(request.HotspotId, request.UserId, cancellationToken);
        if (hotspot == null)
        {
            return OperationResult<bool>.Failure("not_found", "Hotspot bulunamadı");
        }

        _hotspotRepository.RemoveHotspot(hotspot);
        await _hotspotRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}
