using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> GetCustomersByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Customer?> FindByPhoneAsync(Guid userId, string normalizedPhone, CancellationToken cancellationToken);

    Task<Customer?> FindByEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken);

    Task<Customer?> FindByPhoneOrEmailAsync(
        Guid userId,
        string normalizedPhone,
        string? normalizedEmail,
        CancellationToken cancellationToken);

    Task<Customer?> GetByPublicSessionAsync(
        Guid userId,
        string sessionToken,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> GetOrdersByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Order?> GetOrderDetailByCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerMachine>> GetMachinesByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);

    Task<CustomerMachine?> GetMachineByIdAsync(Guid machineId, Guid customerId, CancellationToken cancellationToken);

    Task AddMachineAsync(CustomerMachine machine, CancellationToken cancellationToken);

    void RemoveMachine(CustomerMachine machine);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
