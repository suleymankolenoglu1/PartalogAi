namespace Katalogcu.Application.Features.Chat.Common;

public sealed class VisualFeedbackResultDto
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public object? Record { get; init; }
}
