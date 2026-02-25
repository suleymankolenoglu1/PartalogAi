using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegister;

public sealed class PublicRegisterCustomerCommandHandler : IRequestHandler<PublicRegisterCustomerCommand, OperationResult<PublicRegisterCustomerResponse>>
{
    private readonly ICustomerRepository _customerRepository;

    public PublicRegisterCustomerCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicRegisterCustomerResponse>> Handle(PublicRegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerAuthHelpers.NormalizePhone(request.Phone);
        var normalizedEmail = CustomerAuthHelpers.NormalizeEmail(request.Email);

        var customer = await _customerRepository.FindByPhoneOrEmailAsync(
            request.OwnerUserId,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);

        var isNew = customer == null;
        if (isNew)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserId = request.OwnerUserId,
                CreatedDate = DateTime.UtcNow
            };
            await _customerRepository.AddCustomerAsync(customer, cancellationToken);
        }

        customer!.FullName = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.NormalizedPhone = string.IsNullOrWhiteSpace(normalizedPhone) ? customer.NormalizedPhone : normalizedPhone;
        customer.Email = string.IsNullOrWhiteSpace(normalizedEmail) ? customer.Email : normalizedEmail;
        customer.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? customer.CompanyName : request.CompanyName.Trim();
        customer.Note = string.IsNullOrWhiteSpace(request.Note) ? customer.Note : request.Note.Trim();
        customer.LastVisitDate = DateTime.UtcNow;
        customer.IsActive = true;
        customer.UpdatedDate = DateTime.UtcNow;

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<PublicRegisterCustomerResponse>.Success(new PublicRegisterCustomerResponse
        {
            Success = true,
            Created = isNew,
            CustomerId = customer.Id,
            Message = isNew ? "Müşteri kaydı oluşturuldu." : "Müşteri bilgisi güncellendi."
        });
    }
}
