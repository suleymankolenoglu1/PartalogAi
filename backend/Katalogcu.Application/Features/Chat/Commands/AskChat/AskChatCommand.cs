using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Commands.AskChat;

public sealed record AskChatCommand(
    string? UserText,
    string? AiAnswer,
    string? DebugIntentJson,
    IReadOnlyCollection<Guid> CatalogIds,
    IReadOnlyCollection<ChatSourceInput> Sources)
    : IRequest<OperationResult<AskChatResponse>>;

public sealed class AskChatResponse
{
    public string ReplySuggestion { get; init; } = string.Empty;
    public IReadOnlyList<EnrichedPartDto> Products { get; init; } = [];
    public IReadOnlyList<ChatCompareGroupDto>? CompareGroups { get; init; }
    public string? DebugInfo { get; init; }
}
