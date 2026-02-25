using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Hotspots.Commands.DetectHotspots;

public sealed record DetectHotspotsCommand(Guid PageId) : IRequest<OperationResult<DetectHotspotsResponse>>;

public sealed class DetectHotspotsResponse
{
    public Guid PageId { get; init; }
    public int DetectedCount { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<Hotspot> Hotspots { get; init; } = [];
}
