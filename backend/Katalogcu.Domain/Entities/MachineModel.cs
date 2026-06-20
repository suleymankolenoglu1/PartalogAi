using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class MachineModel : BaseEntity
{
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? MachineGroup { get; set; }
    public string? AliasesJson { get; set; }

    public ICollection<PartCompatibilityRule> CompatibilityRules { get; set; } = [];
}
