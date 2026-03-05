using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.UpdateHotspot;

public sealed record UpdateHotspotCommand(
    Guid HotspotId,
    Guid UserId,
    double Left,
    double Top,
    double Width,
    double Height,
    string? Label,
    Guid? ProductId)
    : IRequest<OperationResult<Hotspot>>;
