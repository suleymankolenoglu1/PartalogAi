using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Application.Features.Chat.Common;

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
        var request = new VisualFeedbackRequestDto
        {
            Image = UploadedFile.FromBytes(
                input.ImageBytes,
                input.FileName,
                contentType: string.IsNullOrWhiteSpace(input.ContentType) ? "image/jpeg" : input.ContentType),
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
