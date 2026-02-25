using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;

namespace Katalogcu.Application.Features.Ai.Commands.ExtractTableFromFile;

public sealed class ExtractTableFromFileCommandHandler
    : IRequestHandler<ExtractTableFromFileCommand, OperationResult<IReadOnlyList<ProductItemDto>>>
{
    private readonly IPartalogAiService _partalogAiService;

    public ExtractTableFromFileCommandHandler(IPartalogAiService partalogAiService)
    {
        _partalogAiService = partalogAiService;
    }

    public async Task<OperationResult<IReadOnlyList<ProductItemDto>>> Handle(
        ExtractTableFromFileCommand request,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var table = await _partalogAiService.ExtractTableAsync(bytes, request.PageNumber);
        return OperationResult<IReadOnlyList<ProductItemDto>>.Success(table);
    }
}
