using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.SetPortalCustomerAccess;

public sealed class SetPortalCustomerAccessCommandHandler
    : IRequestHandler<SetPortalCustomerAccessCommand, OperationResult<CustomerListItemDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public SetPortalCustomerAccessCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<CustomerListItemDto>> Handle(
        SetPortalCustomerAccessCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetCustomerByIdAsync(
            request.OwnerUserId,
            request.CustomerId,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<CustomerListItemDto>.Failure("not_found", "Müşteri bulunamadı.");
        }

        customer.IsActive = request.IsActive;
        customer.UpdatedDate = DateTime.UtcNow;

        if (!request.IsActive)
        {
            customer.PublicSessionToken = null;
            customer.PublicSessionExpiresAt = null;
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerListItemDto>.Success(ToDto(customer));
    }

    private static CustomerListItemDto ToDto(Customer customer)
    {
        return new CustomerListItemDto
        {
            Id = customer.Id,
            Name = customer.FullName,
            Company = customer.CompanyName,
            Email = customer.Email,
            Phone = customer.Phone,
            OrderCount = customer.OrderCount,
            TotalSpent = customer.TotalSpent,
            LastVisitDate = customer.LastVisitDate,
            LastOrderDate = customer.LastOrderDate,
            LastLoginDate = customer.LastLoginDate,
            LastActivityDate = customer.LastLoginDate ?? customer.LastOrderDate ?? customer.LastVisitDate,
            HasPassword = !string.IsNullOrWhiteSpace(customer.PasswordHash) && !string.IsNullOrWhiteSpace(customer.PasswordSalt),
            Status = customer.IsActive ? "active" : "inactive",
            Note = customer.Note,
            CreatedDate = customer.CreatedDate
        };
    }
}
