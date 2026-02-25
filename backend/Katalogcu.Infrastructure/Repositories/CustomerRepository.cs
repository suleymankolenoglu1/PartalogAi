using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;

    public CustomerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastVisitDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Customer?> FindByPhoneAsync(Guid userId, string normalizedPhone, CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(
            c => c.UserId == userId && c.NormalizedPhone == normalizedPhone,
            cancellationToken);
    }

    public Task<Customer?> FindByEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(
            c => c.UserId == userId && c.Email != null && c.Email.ToLower() == normalizedEmail,
            cancellationToken);
    }

    public async Task<Customer?> FindByPhoneOrEmailAsync(
        Guid userId,
        string normalizedPhone,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            customer = await FindByPhoneAsync(userId, normalizedPhone, cancellationToken);
        }

        if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            customer = await FindByEmailAsync(userId, normalizedEmail, cancellationToken);
        }

        return customer;
    }

    public Task<Customer?> GetByPublicSessionAsync(
        Guid userId,
        string sessionToken,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        return _context.Customers.FirstOrDefaultAsync(c =>
            c.UserId == userId &&
            c.PublicSessionToken == sessionToken &&
            c.PublicSessionExpiresAt != null &&
            c.PublicSessionExpiresAt > nowUtc,
            cancellationToken);
    }

    public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        return _context.Customers.AddAsync(customer, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<Order>> GetOrdersByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderDetailByCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        return _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
