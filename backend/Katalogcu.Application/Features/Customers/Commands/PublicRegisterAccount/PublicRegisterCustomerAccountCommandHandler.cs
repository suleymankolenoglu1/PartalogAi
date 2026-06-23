using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;

public sealed class PublicRegisterCustomerAccountCommandHandler : IRequestHandler<PublicRegisterCustomerAccountCommand, OperationResult<PublicCustomerAuthResponse>>
{
    private readonly ICustomerRepository _customerRepository;

    public PublicRegisterCustomerAccountCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerAuthResponse>> Handle(PublicRegisterCustomerAccountCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerAuthHelpers.NormalizePhone(request.Phone);
        var normalizedEmail = CustomerAuthHelpers.NormalizeEmail(request.Email);

        var existing = await _customerRepository.FindByPhoneOrEmailAsync(
            request.OwnerUserId,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);

        if (existing == null)
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure(
                "not_found",
                "Bu telefon/e-posta için portal daveti bulunamadı. Lütfen işletme ile iletişime geçin.");
        }

        if (!existing.IsActive)
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure(
                "inactive",
                "Bu portal kullanıcısının erişimi pasif durumda.");
        }

        if (!string.IsNullOrWhiteSpace(existing.PasswordHash) && !string.IsNullOrWhiteSpace(existing.PasswordSalt))
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure(
                "conflict",
                "Bu telefon/e-posta ile kayıtlı hesap zaten var. Lütfen giriş yapın.");
        }

        var customer = existing;

        customer.FullName = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.NormalizedPhone = normalizedPhone;
        customer.Email = string.IsNullOrWhiteSpace(normalizedEmail) ? customer.Email : normalizedEmail;
        customer.LastVisitDate = DateTime.UtcNow;
        customer.LastLoginDate = DateTime.UtcNow;
        customer.IsActive = true;
        customer.UpdatedDate = DateTime.UtcNow;

        CustomerAuthHelpers.CreatePasswordHash(request.Password, out var hash, out var salt);
        customer.PasswordHash = hash;
        customer.PasswordSalt = salt;
        customer.FailedLoginCount = 0;
        customer.LoginLockoutUntil = null;
        customer.LoginCode = null;
        customer.LoginCodeExpiresAt = null;
        customer.PublicSessionToken = CustomerAuthHelpers.CreateSessionToken();
        customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<PublicCustomerAuthResponse>.Success(new PublicCustomerAuthResponse
        {
            Success = true,
            SessionToken = customer.PublicSessionToken ?? string.Empty,
            Customer = new PublicCustomerDto
            {
                Id = customer.Id,
                Name = customer.FullName,
                Phone = customer.Phone,
                Email = customer.Email,
                Company = customer.CompanyName
            }
        });
    }
}
