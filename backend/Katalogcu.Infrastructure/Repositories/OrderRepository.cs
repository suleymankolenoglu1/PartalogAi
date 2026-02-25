using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Order?> GetOrderByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<Product?> GetProductByIdWithCatalogAsync(Guid productId, CancellationToken cancellationToken)
    {
        return _context.Products
            .Include(p => p.Catalog)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
    }

    public Task<Product?> GetLatestProductByCodeAsync(
        string code,
        Guid? publicUserId,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Catalog)
            .Where(p => p.Code == code);

        if (publicUserId.HasValue && publicUserId.Value != Guid.Empty)
        {
            query = query.Where(p => p.Catalog != null && p.Catalog.UserId == publicUserId.Value && p.Catalog.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(p => allowedCatalogIds.Contains(p.CatalogId));
            }
        }

        return query
            .OrderByDescending(p => p.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> ResolveOwnerUserIdAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? ownerUserIdHint,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return ownerUserIdHint ?? Guid.Empty;
        }

        var ownerUserId = await (
            from p in _context.Products.AsNoTracking()
            join c in _context.Catalogs.AsNoTracking() on p.CatalogId equals c.Id
            where productIds.Contains(p.Id)
            select c.UserId
        ).FirstOrDefaultAsync(cancellationToken);

        return ownerUserId == Guid.Empty ? ownerUserIdHint ?? Guid.Empty : ownerUserId;
    }

    public Task<Customer?> GetCustomerByPhoneAsync(Guid userId, string normalizedPhone, CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(
            c => c.UserId == userId && c.NormalizedPhone == normalizedPhone,
            cancellationToken);
    }

    public Task<Customer?> GetCustomerByEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(
            c => c.UserId == userId && c.Email != null && c.Email.ToLower() == normalizedEmail,
            cancellationToken);
    }

    public Task<Customer?> GetCustomerByPublicSessionAsync(
        Guid userId,
        string publicSessionToken,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(c =>
            c.UserId == userId &&
            c.PublicSessionToken == publicSessionToken &&
            c.PublicSessionExpiresAt != null &&
            c.PublicSessionExpiresAt > nowUtc,
            cancellationToken);
    }

    public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        return _context.Customers.AddAsync(customer, cancellationToken).AsTask();
    }

    public Task AddOrderAsync(Order order, CancellationToken cancellationToken)
    {
        return _context.Orders.AddAsync(order, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<Order>> GetIncomingOrdersForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Catalog)
            .Include(o => o.StatusHistory)
            .Where(o =>
                o.OwnerUserId == userId ||
                o.Items.Any(i => i.Product != null && i.Product.Catalog != null && i.Product.Catalog.UserId == userId) ||
                (o.CustomerId != null && _context.Customers.Any(c => c.Id == o.CustomerId && c.UserId == userId)))
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Catalog)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public Task<Order?> GetOrderByIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Catalog)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o =>
                o.Id == orderId &&
                (o.OwnerUserId == userId ||
                 o.Items.Any(i => i.Product != null && i.Product.Catalog != null && i.Product.Catalog.UserId == userId) ||
                 (o.CustomerId != null && _context.Customers.Any(c => c.Id == o.CustomerId && c.UserId == userId))),
                cancellationToken);
    }

    public Task<bool> IsCustomerOwnedByUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Customers.AnyAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
