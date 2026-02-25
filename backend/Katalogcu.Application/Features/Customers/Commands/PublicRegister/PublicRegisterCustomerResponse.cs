namespace Katalogcu.Application.Features.Customers.Commands.PublicRegister;

public sealed class PublicRegisterCustomerResponse
{
    public bool Success { get; init; }
    public bool Created { get; init; }
    public Guid CustomerId { get; init; }
    public string Message { get; init; } = string.Empty;
}
