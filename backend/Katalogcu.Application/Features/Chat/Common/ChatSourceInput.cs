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
    public double? Similarity { get; init; }
    public string? MatchReason { get; init; }
    public string? ConfidenceLabel { get; init; }
    public bool? RequiresVerification { get; init; }
    public bool? Fallback { get; init; }
    public string? FallbackReason { get; init; }
    public Guid? CatalogId { get; init; }
    public string? PageNumber { get; init; }
    public int? Quantity { get; init; }
    public string? RequestedMachineBrand { get; init; }
    public string? RequestedMachineModel { get; init; }
    public string? RequestedMachineVariant { get; init; }
}
