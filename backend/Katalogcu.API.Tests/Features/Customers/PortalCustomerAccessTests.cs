using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Customers.Commands.PublicLogin;
using Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;
using Katalogcu.Application.Features.Customers.Commands.SetPortalCustomerAccess;
using Katalogcu.Domain.Entities;
using Xunit;

namespace Katalogcu.API.Tests.Features.Customers;

public sealed class PortalCustomerAccessTests
{
    [Fact]
    public async Task CompleteAccount_RejectsCustomerWithoutPortalInvite()
    {
        var repository = new FakeCustomerRepository();
        var handler = new PublicRegisterCustomerAccountCommandHandler(repository);

        var result = await handler.Handle(new PublicRegisterCustomerAccountCommand(
            Guid.NewGuid(),
            "Test Customer",
            "555 000 00 00",
            "customer@example.test",
            "StrongPass123"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.ErrorCode);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CompleteAccount_SetsPasswordAndSessionForInvitedCustomer()
    {
        var ownerId = Guid.NewGuid();
        var customer = CreateCustomer(ownerId);
        var repository = new FakeCustomerRepository(customer);
        var handler = new PublicRegisterCustomerAccountCommandHandler(repository);

        var result = await handler.Handle(new PublicRegisterCustomerAccountCommand(
            ownerId,
            "Updated Customer",
            customer.Phone,
            customer.Email,
            "StrongPass123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Customer", customer.FullName);
        Assert.False(string.IsNullOrWhiteSpace(customer.PasswordHash));
        Assert.False(string.IsNullOrWhiteSpace(customer.PasswordSalt));
        Assert.False(string.IsNullOrWhiteSpace(customer.PublicSessionToken));
        Assert.True(customer.PublicSessionExpiresAt > DateTime.UtcNow);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Login_RejectsInactiveCustomer()
    {
        var ownerId = Guid.NewGuid();
        var customer = CreateCustomer(ownerId);
        customer.IsActive = false;
        var repository = new FakeCustomerRepository(customer);
        var handler = new PublicCustomerLoginCommandHandler(repository);

        var result = await handler.Handle(new PublicCustomerLoginCommand(
            ownerId,
            customer.Phone,
            customer.Email,
            "StrongPass123"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("inactive", result.ErrorCode);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task SetAccess_DisablingCustomerClearsPublicSession()
    {
        var ownerId = Guid.NewGuid();
        var customer = CreateCustomer(ownerId);
        customer.PublicSessionToken = "existing-session";
        customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(7);
        var repository = new FakeCustomerRepository(customer);
        var handler = new SetPortalCustomerAccessCommandHandler(repository);

        var result = await handler.Handle(new SetPortalCustomerAccessCommand(
            ownerId,
            customer.Id,
            false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(customer.IsActive);
        Assert.Null(customer.PublicSessionToken);
        Assert.Null(customer.PublicSessionExpiresAt);
        Assert.Equal("inactive", result.Value?.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    private static Customer CreateCustomer(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = ownerId,
        FullName = "Test Customer",
        Phone = "555 111 22 33",
        NormalizedPhone = "5551112233",
        Email = "customer@example.test",
        IsActive = true,
        CreatedDate = DateTime.UtcNow.AddDays(-1),
        LastVisitDate = DateTime.UtcNow.AddDays(-1)
    };

    private sealed class FakeCustomerRepository(params Customer[] customers) : ICustomerRepository
    {
        private readonly List<Customer> _customers = customers.ToList();

        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<Customer>> GetCustomersByUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Customer>>(_customers.Where(customer => customer.UserId == userId).ToList());

        public Task<Customer?> GetCustomerByIdAsync(Guid userId, Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult(_customers.FirstOrDefault(customer => customer.UserId == userId && customer.Id == customerId));

        public Task<Customer?> FindByPhoneAsync(Guid userId, string normalizedPhone, CancellationToken cancellationToken)
            => Task.FromResult(_customers.FirstOrDefault(customer =>
                customer.UserId == userId &&
                customer.NormalizedPhone == normalizedPhone));

        public Task<Customer?> FindByEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(_customers.FirstOrDefault(customer =>
                customer.UserId == userId &&
                string.Equals(customer.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)));

        public Task<Customer?> FindByPhoneOrEmailAsync(
            Guid userId,
            string normalizedPhone,
            string? normalizedEmail,
            CancellationToken cancellationToken)
            => Task.FromResult(_customers.FirstOrDefault(customer =>
                customer.UserId == userId &&
                (customer.NormalizedPhone == normalizedPhone ||
                    (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                        string.Equals(customer.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))));

        public Task<Customer?> GetByPublicSessionAsync(
            Guid userId,
            string sessionToken,
            DateTime nowUtc,
            CancellationToken cancellationToken)
            => Task.FromResult(_customers.FirstOrDefault(customer =>
                customer.UserId == userId &&
                customer.IsActive &&
                customer.PublicSessionToken == sessionToken &&
                customer.PublicSessionExpiresAt > nowUtc));

        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Order>> GetOrdersByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Order>>(Array.Empty<Order>());

        public Task<Order?> GetOrderDetailByCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult<Order?>(null);

        public Task<IReadOnlyList<CustomerMachine>> GetMachinesByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerMachine>>(Array.Empty<CustomerMachine>());

        public Task<CustomerMachine?> GetMachineByIdAsync(Guid machineId, Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult<CustomerMachine?>(null);

        public Task AddMachineAsync(CustomerMachine machine, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void RemoveMachine(CustomerMachine machine)
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
