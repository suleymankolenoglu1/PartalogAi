using Katalogcu.Application.Features.Chat.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IVisualFeedbackService
{
    Task<VisualFeedbackResultDto> SaveAsync(VisualFeedbackInputDto input, CancellationToken cancellationToken);
}
