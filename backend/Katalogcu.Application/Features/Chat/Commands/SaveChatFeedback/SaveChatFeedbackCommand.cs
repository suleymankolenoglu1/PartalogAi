using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Commands.SaveChatFeedback;

public sealed record SaveChatFeedbackCommand(
    Guid UserId,
    bool IsPublic,
    bool Helpful,
    string? Reason,
    string? UserQuery,
    string ReplySuggestion,
    IReadOnlyCollection<string>? SourceCodes,
    IReadOnlyCollection<string>? CatalogIds,
    string? PublicToken,
    string? ContextJson,
    string? MessageId,
    string? ConversationId,
    string? UserAgent,
    string? IpAddress)
    : IRequest<OperationResult<SaveChatFeedbackResponse>>;

public sealed class SaveChatFeedbackResponse
{
    public string Id { get; init; } = string.Empty;
}
