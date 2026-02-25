namespace Katalogcu.Application.Common.Models;

public sealed class PublicAccessPayloadDto
{
    public Guid UserId { get; init; }
    public IReadOnlyList<Guid> CatalogIds { get; init; } = [];
}
