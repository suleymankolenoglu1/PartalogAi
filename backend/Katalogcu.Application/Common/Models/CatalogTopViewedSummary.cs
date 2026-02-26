namespace Katalogcu.Application.Common.Models;

public sealed class CatalogTopViewedSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ViewCount { get; init; }
    public DateTime LastViewedAtUtc { get; init; }
}
