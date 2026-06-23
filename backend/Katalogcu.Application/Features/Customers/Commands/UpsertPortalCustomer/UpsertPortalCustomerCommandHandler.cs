using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.UpsertPortalCustomer;

public sealed class UpsertPortalCustomerCommandHandler
    : IRequestHandler<UpsertPortalCustomerCommand, OperationResult<CustomerListItemDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public UpsertPortalCustomerCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<CustomerListItemDto>> Handle(
        UpsertPortalCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerAuthHelpers.NormalizePhone(request.Phone);
        var normalizedEmail = CustomerAuthHelpers.NormalizeEmail(request.Email);

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetCustomerByIdAsync(
                request.OwnerUserId,
                request.CustomerId.Value,
                cancellationToken);

            if (customer == null)
            {
                return OperationResult<CustomerListItemDto>.Failure("not_found", "Müşteri bulunamadı.");
            }
        }

        var duplicate = await _customerRepository.FindByPhoneOrEmailAsync(
            request.OwnerUserId,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);

        if (duplicate != null && (customer == null || duplicate.Id != customer.Id))
        {
            return OperationResult<CustomerListItemDto>.Failure(
                "conflict",
                "Bu telefon/e-posta ile kayıtlı başka bir portal kullanıcısı var.");
        }

        var now = DateTime.UtcNow;
        customer ??= new Customer
        {
            Id = Guid.NewGuid(),
            UserId = request.OwnerUserId,
            CreatedDate = now,
            LastVisitDate = now
        };

        customer.FullName = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.NormalizedPhone = normalizedPhone;
        customer.Email = normalizedEmail;
        customer.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
        customer.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        customer.IsActive = request.IsActive;
        customer.UpdatedDate = now;

        if (!request.IsActive)
        {
            customer.PublicSessionToken = null;
            customer.PublicSessionExpiresAt = null;
        }

        if (!string.IsNullOrWhiteSpace(request.InitialPassword))
        {
            CustomerAuthHelpers.CreatePasswordHash(request.InitialPassword, out var hash, out var salt);
            customer.PasswordHash = hash;
            customer.PasswordSalt = salt;
            customer.FailedLoginCount = 0;
            customer.LoginLockoutUntil = null;
            customer.LoginCode = null;
            customer.LoginCodeExpiresAt = null;
        }

        if (!request.CustomerId.HasValue)
        {
            await _customerRepository.AddCustomerAsync(customer, cancellationToken);
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
