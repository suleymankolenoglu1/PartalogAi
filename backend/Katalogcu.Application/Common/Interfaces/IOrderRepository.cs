using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetOrderByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<Product?> GetProductByIdWithCatalogAsync(Guid productId, CancellationToken cancellationToken);

    Task<Product?> GetLatestProductByCodeAsync(
        string code,
        Guid? publicUserId,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken);

    Task<Guid> ResolveOwnerUserIdAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? ownerUserIdHint,
        CancellationToken cancellationToken);

    Task<Customer?> GetCustomerByPhoneAsync(Guid userId, string normalizedPhone, CancellationToken cancellationToken);

    Task<Customer?> GetCustomerByEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken);

    Task<Customer?> GetCustomerByPublicSessionAsync(
        Guid userId,
        string publicSessionToken,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken);

    Task AddOrderAsync(Order order, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> GetIncomingOrdersForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Order?> GetOrderByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> GetOrderByIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken);

    Task<bool> IsCustomerOwnedByUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
