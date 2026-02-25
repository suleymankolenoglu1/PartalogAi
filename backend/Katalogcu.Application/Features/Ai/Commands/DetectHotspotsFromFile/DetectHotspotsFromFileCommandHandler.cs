using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Ai.Commands.DetectHotspotsFromFile;

public sealed class DetectHotspotsFromFileCommandHandler
    : IRequestHandler<DetectHotspotsFromFileCommand, OperationResult<IReadOnlyList<Hotspot>>>
{
    private readonly IPartalogAiService _partalogAiService;

    public DetectHotspotsFromFileCommandHandler(IPartalogAiService partalogAiService)
    {
        _partalogAiService = partalogAiService;
    }

    public async Task<OperationResult<IReadOnlyList<Hotspot>>> Handle(
        DetectHotspotsFromFileCommand request,
        CancellationToken cancellationToken)
    {
        var hotspots = await _partalogAiService.DetectHotspotsAsync(request.File, request.PageId);
        return OperationResult<IReadOnlyList<Hotspot>>.Success(hotspots);
    }
}
