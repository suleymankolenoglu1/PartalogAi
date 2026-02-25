namespace Katalogcu.Application.Common.Models;

public sealed class CatalogRecentSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int PartCount { get; init; }
    public DateTime CreatedDate { get; init; }
}
