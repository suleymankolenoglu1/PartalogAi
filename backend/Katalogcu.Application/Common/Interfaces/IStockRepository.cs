using Katalogcu.Application.Features.Products.Queries.Common;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IStockRepository
{
    Task<bool> UserOwnsCatalogAsync(Guid userId, Guid catalogId, CancellationToken cancellationToken);

    Task<Product?> GetProductWithCatalogAsync(Guid productId, CancellationToken cancellationToken);

    Task<Product?> GetOwnedProductAsync(Guid productId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetOwnedProductsAsync(
        Guid userId,
        Guid? catalogId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetOwnedProductsForListAsync(Guid userId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ProductListItemDto> Items, int TotalCount)> GetOwnedProductsPageAsync(
        Guid userId,
        Guid? catalogId,
        string? stockStatus,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetCatalogProductsForListAsync(
        Guid userId,
        Guid catalogId,
        bool publishedOnly,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken);

    Task AddProductAsync(Product product, CancellationToken cancellationToken);
    Task AddProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken);
    Task<IReadOnlyList<Hotspot>> GetHotspotsByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderItem>> GetOrderItemsByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    void RemoveHotspots(IEnumerable<Hotspot> hotspots);
    void RemoveOrderItems(IEnumerable<OrderItem> orderItems);
    void RemoveProduct(Product product);

    Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(
        Guid userId,
        Guid? productId,
        int limit,
        CancellationToken cancellationToken);

    Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken);

    Task AddStockMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
