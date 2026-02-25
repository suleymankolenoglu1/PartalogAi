namespace Katalogcu.Application.Features.Chat.Common;

public sealed class VisualFeedbackInputDto
{
    public Guid UserId { get; init; }
    public byte[] ImageBytes { get; init; } = [];
    public string FileName { get; init; } = "image.jpg";
    public string ContentType { get; init; } = "image/jpeg";
    public string? PartName { get; init; }
    public string? PartCode { get; init; }
    public string? MachineBrand { get; init; }
    public string? MachineType { get; init; }
    public string? Note { get; init; }
}
