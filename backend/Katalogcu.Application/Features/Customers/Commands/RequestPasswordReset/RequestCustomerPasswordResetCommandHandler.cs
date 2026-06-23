using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;

public sealed class RequestCustomerPasswordResetCommandHandler : IRequestHandler<RequestCustomerPasswordResetCommand, OperationResult<RequestCustomerPasswordResetResponse>>
{
    private readonly ICustomerRepository _customerRepository;

    public RequestCustomerPasswordResetCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<RequestCustomerPasswordResetResponse>> Handle(RequestCustomerPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerAuthHelpers.NormalizePhone(request.Phone);
        var normalizedEmail = CustomerAuthHelpers.NormalizeEmail(request.Email);

        var customer = await _customerRepository.FindByPhoneOrEmailAsync(
            request.OwnerUserId,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);

        string? resetCode = null;
        if (customer is { IsActive: true })
        {
            resetCode = CustomerAuthHelpers.GenerateResetCode();
            customer.LoginCode = resetCode;
            customer.LoginCodeExpiresAt = DateTime.UtcNow.Add(CustomerAuthHelpers.ResetCodeDuration);
            customer.UpdatedDate = DateTime.UtcNow;
            await _customerRepository.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<RequestCustomerPasswordResetResponse>.Success(new RequestCustomerPasswordResetResponse
        {
            Success = true,
            Message = "Şifre sıfırlama kodu oluşturuldu.",
            ResetCode = IsDebugEnabled() ? resetCode : null
        });
    }

    private static bool IsDebugEnabled()
    {
        var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.Equals(aspnetEnv, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var raw = Environment.GetEnvironmentVariable("DEBUG");
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
               || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
