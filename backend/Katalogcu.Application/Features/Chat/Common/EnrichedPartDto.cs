namespace Katalogcu.Application.Features.Chat.Common;

public sealed class EnrichedPartDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? RefNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Model { get; init; }
    public string? Brand { get; init; }
    public Guid CatalogId { get; init; }
    public string? PageNumber { get; init; }
    public string? StockStatus { get; init; }
    public decimal? Price { get; init; }
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }
    public string? SourceQuery { get; init; }
    public double? SourceSimilarity { get; init; }
    public string? MatchReason { get; init; }
    public string? ConfidenceLabel { get; init; }
    public bool? RequiresVerification { get; init; }
    public bool? Fallback { get; init; }
    public string? FallbackReason { get; init; }
    public string? CompatibilityLevel { get; init; }
    public string? CompatibilitySourceType { get; init; }
    public decimal? CompatibilityConfidence { get; init; }
    public string? CompatibilityMachineLabel { get; init; }
    public string? CompatibilityNotes { get; init; }
}
