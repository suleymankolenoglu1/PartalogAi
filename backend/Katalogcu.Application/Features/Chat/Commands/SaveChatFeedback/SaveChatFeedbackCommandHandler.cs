using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Commands.SaveChatFeedback;

public sealed class SaveChatFeedbackCommandHandler : IRequestHandler<SaveChatFeedbackCommand, OperationResult<SaveChatFeedbackResponse>>
{
    private readonly IChatFeedbackStore _chatFeedbackStore;

    public SaveChatFeedbackCommandHandler(IChatFeedbackStore chatFeedbackStore)
    {
        _chatFeedbackStore = chatFeedbackStore;
    }

    public async Task<OperationResult<SaveChatFeedbackResponse>> Handle(SaveChatFeedbackCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var sourceCodes = (request.SourceCodes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .Take(30)
            .ToList();

        var entry = new ChatFeedbackEntry
        {
            Id = id,
            CreatedAt = DateTime.UtcNow,
            UserId = request.UserId,
            IsPublic = request.IsPublic,
            Helpful = request.Helpful,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            UserQuery = string.IsNullOrWhiteSpace(request.UserQuery) ? null : request.UserQuery.Trim(),
            ReplySuggestion = request.ReplySuggestion.Trim(),
            SourceCodes = sourceCodes,
            MessageId = string.IsNullOrWhiteSpace(request.MessageId) ? null : request.MessageId.Trim(),
            ConversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? null : request.ConversationId.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress.Trim()
        };

        await _chatFeedbackStore.SaveAsync(entry, cancellationToken);

        return OperationResult<SaveChatFeedbackResponse>.Success(new SaveChatFeedbackResponse
        {
            Id = id
        });
    }
}
