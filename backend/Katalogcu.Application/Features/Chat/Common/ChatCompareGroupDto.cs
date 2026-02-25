namespace Katalogcu.Application.Features.Chat.Common;

public sealed class ChatCompareGroupDto
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<EnrichedPartDto> Results { get; init; } = [];
}
