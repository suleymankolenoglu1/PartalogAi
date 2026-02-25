namespace Katalogcu.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductResponse
{
    public Guid Id { get; init; }
    public Guid CatalogId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? OemNo { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public string? ImageUrl { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PageNumber { get; init; } = string.Empty;
    public int RefNo { get; init; }
    public DateTime CreatedDate { get; init; }
}
