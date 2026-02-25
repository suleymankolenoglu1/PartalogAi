using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;

public sealed record CreateHotspotCommand(
    Guid PageId,
    double Left,
    double Top,
    double Width,
    double Height,
    string? Label,
    bool IsAiDetected,
    double AiConfidence,
    Guid? ProductId)
    : IRequest<OperationResult<Hotspot>>;
