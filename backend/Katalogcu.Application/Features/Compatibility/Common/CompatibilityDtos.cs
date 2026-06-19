namespace Katalogcu.Application.Features.Compatibility.Common;

public sealed class MachineModelDto
{
    public Guid Id { get; init; }
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string? Variant { get; init; }
    public string? MachineGroup { get; init; }
    public string? AliasesJson { get; init; }
}

public sealed class PartCompatibilityRuleDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public Guid MachineModelId { get; init; }
    public string MachineLabel { get; init; } = string.Empty;
    public string CompatibilityLevel { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public string? Notes { get; init; }
}
