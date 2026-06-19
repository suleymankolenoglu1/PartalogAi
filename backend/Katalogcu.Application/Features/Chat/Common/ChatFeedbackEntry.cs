namespace Katalogcu.Application.Features.Chat.Common;

public sealed class ChatFeedbackEntry
{
    public string Id { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Guid UserId { get; init; }
    public bool IsPublic { get; init; }
    public bool Helpful { get; init; }
    public string? Reason { get; init; }
    public string? UserQuery { get; init; }
    public string ReplySuggestion { get; init; } = string.Empty;
    public IReadOnlyList<string> SourceCodes { get; init; } = [];
    public string? MessageId { get; init; }
    public string? ConversationId { get; init; }
    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
}
