using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;

namespace Katalogcu.Application.Features.Ai.Commands.AnalyzePageFromFile;

public sealed class AnalyzePageFromFileCommandHandler
    : IRequestHandler<AnalyzePageFromFileCommand, OperationResult<PageAnalysisResult>>
{
    private readonly IPartalogAiService _partalogAiService;

    public AnalyzePageFromFileCommandHandler(IPartalogAiService partalogAiService)
    {
        _partalogAiService = partalogAiService;
    }

    public async Task<OperationResult<PageAnalysisResult>> Handle(
        AnalyzePageFromFileCommand request,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var analysis = await _partalogAiService.AnalyzePageAsync(bytes);
        return OperationResult<PageAnalysisResult>.Success(analysis);
    }
}
