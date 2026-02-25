namespace Katalogcu.Application.Features.Products.Queries.Common;

public sealed class ProductListItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? OemNo { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public string? ImageUrl { get; init; }
    public string Category { get; init; } = string.Empty;
    public string CatalogName { get; init; } = "Genel Stok";
    public Guid CatalogId { get; init; }
}
