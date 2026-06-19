using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Products.Queries.Common;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly AppDbContext _context;

    public StockRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> UserOwnsCatalogAsync(Guid userId, Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.AnyAsync(c => c.Id == catalogId && c.UserId == userId, cancellationToken);
    }

    public Task<Product?> GetProductWithCatalogAsync(Guid productId, CancellationToken cancellationToken)
    {
        return _context.Products
            .Include(p => p.Catalog)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
    }

    public Task<Product?> GetOwnedProductAsync(Guid productId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Products
            .Include(p => p.Catalog)
            .FirstOrDefaultAsync(
                p => p.Id == productId && p.Catalog != null && p.Catalog.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetOwnedProductsAsync(
        Guid userId,
        Guid? catalogId,
        CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Catalog)
            .Where(p => p.Catalog != null && p.Catalog.UserId == userId);

        if (catalogId.HasValue && catalogId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CatalogId == catalogId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetOwnedProductsForListAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Catalog)
            .Where(p => p.Catalog != null && p.Catalog.UserId == userId)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProductListItemDto> Items, int TotalCount)> GetOwnedProductsPageAsync(
        Guid userId,
        Guid? catalogId,
        string? stockStatus,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Catalog)
            .Where(p => p.Catalog != null && p.Catalog.UserId == userId);

        if (catalogId.HasValue && catalogId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CatalogId == catalogId.Value);
        }

        if (!string.IsNullOrWhiteSpace(stockStatus))
        {
            var normalized = stockStatus.Trim().ToLowerInvariant();
            if (normalized is "in" or "in_stock" or "stocked")
            {
                query = query.Where(p => p.StockQuantity > 0);
            }
            else if (normalized is "out" or "out_of_stock")
            {
                query = query.Where(p => p.StockQuantity <= 0);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term) ||
                (p.OemNo != null && p.OemNo.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                OemNo = p.OemNo,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                Category = p.Category,
                CatalogId = p.CatalogId,
                CatalogName = p.Catalog != null ? p.Catalog.Name : "Genel Stok"
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Product>> GetCatalogProductsForListAsync(
        Guid userId,
        Guid catalogId,
        bool publishedOnly,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Catalog)
            .Where(p => p.CatalogId == catalogId && p.Catalog != null && p.Catalog.UserId == userId);

        if (publishedOnly)
        {
            query = query.Where(p => p.Catalog != null && p.Catalog.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(p => allowedCatalogIds.Contains(p.CatalogId));
            }
        }

        return await query
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }

    public Task AddProductAsync(Product product, CancellationToken cancellationToken)
    {
        return _context.Products.AddAsync(product, cancellationToken).AsTask();
    }

    public Task AddProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken)
    {
        return _context.Products.AddRangeAsync(products, cancellationToken);
    }

    public async Task<IReadOnlyList<Hotspot>> GetHotspotsByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await _context.Hotspots
            .Where(h => h.ProductId == productId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderItem>> GetOrderItemsByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await _context.OrderItems
            .Where(oi => oi.ProductId == productId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveHotspots(IEnumerable<Hotspot> hotspots)
    {
        _context.Hotspots.RemoveRange(hotspots);
    }

    public void RemoveOrderItems(IEnumerable<OrderItem> orderItems)
    {
        _context.OrderItems.RemoveRange(orderItems);
    }

    public void RemoveProduct(Product product)
    {
        _context.Products.Remove(product);
    }

    public async Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(
        Guid userId,
        Guid? productId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _context.StockMovements
            .AsNoTracking()
            .Where(m => m.UserId == userId);

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            query = query.Where(m => m.ProductId == productId.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedDate)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        return _context.StockMovements.AddAsync(movement, cancellationToken).AsTask();
    }

    public Task AddStockMovementsAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken)
    {
        return _context.StockMovements.AddRangeAsync(movements, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
