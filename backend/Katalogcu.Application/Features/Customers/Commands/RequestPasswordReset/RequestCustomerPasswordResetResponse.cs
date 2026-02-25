namespace Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;

public sealed class RequestCustomerPasswordResetResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ResetCode { get; init; }
}
