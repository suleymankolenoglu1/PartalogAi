using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class PolicyThreshold : BaseEntity
{
    public const string GlobalScope = "Global";
    public const string BrandScope = "Brand";
    public const string CatalogScope = "Catalog";

    public string ScopeType { get; set; } = GlobalScope;
    public string ScopeKey { get; set; } = "default";

    public decimal? HighConfidence { get; set; }
    public decimal? LowConfidence { get; set; }
    public decimal? AmbiguityScoreDelta { get; set; }

    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public string? Notes { get; set; }
    public string? UpdatedBy { get; set; }
}
