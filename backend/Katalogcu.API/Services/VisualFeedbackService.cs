using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Application.Features.Chat.Common;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.API.Services;

public sealed class VisualFeedbackService : IVisualFeedbackService
{
    private readonly IPartalogAiService _aiService;

    public VisualFeedbackService(IPartalogAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task<VisualFeedbackResultDto> SaveAsync(VisualFeedbackInputDto input, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(input.ImageBytes);
        var formFile = new FormFile(stream, 0, stream.Length, "file", input.FileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = string.IsNullOrWhiteSpace(input.ContentType) ? "image/jpeg" : input.ContentType
        };

        var request = new VisualFeedbackRequestDto
        {
            Image = formFile,
            PartName = input.PartName,
            PartCode = input.PartCode,
            MachineBrand = input.MachineBrand,
            MachineType = input.MachineType,
            UserId = input.UserId.ToString(),
            Note = input.Note
        };

        var result = await _aiService.SaveVisualFeedbackAsync(request);
        return new VisualFeedbackResultDto
        {
            Success = result.Success,
            Message = result.Message,
            Record = result.Record
        };
    }
}
