namespace Katalogcu.Application.Common.Models;

public sealed class PublicAccessPayloadDto
{
    public Guid UserId { get; init; }
    public IReadOnlyList<Guid> CatalogIds { get; init; } = [];
    public bool IsEmbedSession { get; init; }
    public string? EmbedKey { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}
