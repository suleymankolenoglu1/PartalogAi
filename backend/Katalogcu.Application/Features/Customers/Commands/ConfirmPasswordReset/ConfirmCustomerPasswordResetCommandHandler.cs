using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.ConfirmPasswordReset;

public sealed class ConfirmCustomerPasswordResetCommandHandler : IRequestHandler<ConfirmCustomerPasswordResetCommand, OperationResult<PublicCustomerAuthResponse>>
{
    private readonly ICustomerRepository _customerRepository;

    public ConfirmCustomerPasswordResetCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerAuthResponse>> Handle(ConfirmCustomerPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerAuthHelpers.NormalizePhone(request.Phone);
        var normalizedEmail = CustomerAuthHelpers.NormalizeEmail(request.Email);
        var customer = await _customerRepository.FindByPhoneOrEmailAsync(
            request.OwnerUserId,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure("validation", "Sıfırlama doğrulanamadı.");
        }

        if (!customer.IsActive)
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure("validation", "Bu portal kullanıcısının erişimi pasif durumda.");
        }

        if (string.IsNullOrWhiteSpace(customer.LoginCode) || customer.LoginCodeExpiresAt == null || customer.LoginCodeExpiresAt <= DateTime.UtcNow)
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure("validation", "Doğrulama kodu geçersiz veya süresi dolmuş.");
        }

        if (!string.Equals(customer.LoginCode, request.ResetCode.Trim(), StringComparison.Ordinal))
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure("validation", "Doğrulama kodu hatalı.");
        }

        CustomerAuthHelpers.CreatePasswordHash(request.NewPassword, out var hash, out var salt);
        customer.PasswordHash = hash;
        customer.PasswordSalt = salt;
        customer.LoginCode = null;
        customer.LoginCodeExpiresAt = null;
        customer.FailedLoginCount = 0;
        customer.LoginLockoutUntil = null;
        customer.LastLoginDate = DateTime.UtcNow;
        customer.LastVisitDate = DateTime.UtcNow;
        customer.PublicSessionToken = CustomerAuthHelpers.CreateSessionToken();
        customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);
        customer.UpdatedDate = DateTime.UtcNow;

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
