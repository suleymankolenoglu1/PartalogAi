namespace Katalogcu.Application.Features.Chat.Common;

public sealed class ChatSourceInput
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Model { get; init; }
    public string? LegacyModel { get; init; }
    public string? Description { get; init; }
    public string? LegacyDescription { get; init; }
    public string? Query { get; init; }
    public Guid? CatalogId { get; init; }
    public string? PageNumber { get; init; }
}
