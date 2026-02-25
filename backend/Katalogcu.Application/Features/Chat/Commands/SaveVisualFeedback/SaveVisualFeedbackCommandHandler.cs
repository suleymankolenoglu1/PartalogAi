using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Commands.SaveVisualFeedback;

public sealed class SaveVisualFeedbackCommandHandler : IRequestHandler<SaveVisualFeedbackCommand, OperationResult<VisualFeedbackResultDto>>
{
    private readonly IVisualFeedbackService _visualFeedbackService;

    public SaveVisualFeedbackCommandHandler(IVisualFeedbackService visualFeedbackService)
    {
        _visualFeedbackService = visualFeedbackService;
    }

    public async Task<OperationResult<VisualFeedbackResultDto>> Handle(SaveVisualFeedbackCommand request, CancellationToken cancellationToken)
    {
        var response = await _visualFeedbackService.SaveAsync(new VisualFeedbackInputDto
        {
            UserId = request.UserId,
            ImageBytes = request.ImageBytes,
            FileName = request.FileName,
            ContentType = request.ContentType,
            PartName = request.PartName,
            PartCode = request.PartCode,
            MachineBrand = request.MachineBrand,
            MachineType = request.MachineType,
            Note = request.Note
        }, cancellationToken);

        return OperationResult<VisualFeedbackResultDto>.Success(response);
    }
}
