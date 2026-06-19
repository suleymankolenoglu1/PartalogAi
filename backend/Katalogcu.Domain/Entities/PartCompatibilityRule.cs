using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class PartCompatibilityRule : BaseEntity
{
    public Guid CatalogItemId { get; set; }
    public Guid MachineModelId { get; set; }
    public string CompatibilityLevel { get; set; } = "Unknown";
    public string SourceType { get; set; } = "Manual";
    public decimal Confidence { get; set; }
    public string? Notes { get; set; }

    public CatalogItem? CatalogItem { get; set; }
    public MachineModel? MachineModel { get; set; }
}
