namespace Katalogcu.Application.Features.Chat.Common;

public sealed class ChatFeedbackRequestDto
{
    public bool Helpful { get; set; }
    public string? Reason { get; set; }
    public string? UserQuery { get; set; }
    public string? ReplySuggestion { get; set; }
    public List<string>? SourceCodes { get; set; }
    public List<string>? CatalogIds { get; set; }
    public string? PublicToken { get; set; }
    public string? ContextJson { get; set; }
    public string? MessageId { get; set; }
    public string? ConversationId { get; set; }
}
