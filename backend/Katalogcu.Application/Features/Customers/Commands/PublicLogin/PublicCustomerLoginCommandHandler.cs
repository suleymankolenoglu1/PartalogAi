using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicLogin;

public sealed class PublicCustomerLoginCommandHandler : IRequestHandler<PublicCustomerLoginCommand, OperationResult<PublicCustomerAuthResponse>>
{
    private readonly ICustomerRepository _customerRepository;

    public PublicCustomerLoginCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerAuthResponse>> Handle(PublicCustomerLoginCommand request, CancellationToken cancellationToken)
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
            return OperationResult<PublicCustomerAuthResponse>.Failure("not_found", "Müşteri kaydı bulunamadı. Önce kayıt olmanız gerekiyor.");
        }

        if (customer.LoginLockoutUntil != null && customer.LoginLockoutUntil > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((customer.LoginLockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            return OperationResult<PublicCustomerAuthResponse>.Failure(
                "locked",
                $"Çok fazla hatalı giriş denemesi. Lütfen {remainingMinutes:0} dakika sonra tekrar deneyin.");
        }

        if (string.IsNullOrWhiteSpace(customer.PasswordHash) || string.IsNullOrWhiteSpace(customer.PasswordSalt))
        {
            return OperationResult<PublicCustomerAuthResponse>.Failure(
                "no_password",
                "Bu müşteri için hesap şifresi tanımlı değil. Lütfen kayıt olun.");
        }

        if (!CustomerAuthHelpers.VerifyPassword(request.Password, customer.PasswordHash, customer.PasswordSalt))
        {
            customer.FailedLoginCount += 1;
            customer.UpdatedDate = DateTime.UtcNow;
            if (customer.FailedLoginCount >= CustomerAuthHelpers.MaxFailedLoginAttempts)
            {
                customer.LoginLockoutUntil = DateTime.UtcNow.Add(CustomerAuthHelpers.LoginLockoutDuration);
                customer.FailedLoginCount = 0;
            }

            await _customerRepository.SaveChangesAsync(cancellationToken);
            return OperationResult<PublicCustomerAuthResponse>.Failure("invalid_credentials", "Telefon/e-posta veya şifre hatalı.");
        }

        customer.LastVisitDate = DateTime.UtcNow;
        customer.IsActive = true;
        customer.FailedLoginCount = 0;
        customer.LoginLockoutUntil = null;
        customer.PublicSessionToken = CustomerAuthHelpers.CreateSessionToken();
        customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);
        customer.LastLoginDate = DateTime.UtcNow;
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
