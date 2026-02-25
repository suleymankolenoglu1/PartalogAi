using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.DeleteHotspot;

public sealed record DeleteHotspotCommand(Guid HotspotId) : IRequest<OperationResult<bool>>;
