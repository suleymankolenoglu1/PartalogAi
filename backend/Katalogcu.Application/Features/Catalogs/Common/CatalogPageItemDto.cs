namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class CatalogPageItemDto
{
    public Guid CatalogItemId { get; init; }
    public string RefNo { get; init; } = string.Empty;
    public string PartCode { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsStocked { get; init; }
    public Guid? ProductId { get; init; }
    public decimal? Price { get; init; }
    public string? LocalName { get; init; }
}
