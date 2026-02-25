namespace Katalogcu.Application.Features.Customers.Common;

public sealed class PublicCustomerAuthResponse
{
    public bool Success { get; init; }
    public string SessionToken { get; init; } = string.Empty;
    public PublicCustomerDto Customer { get; init; } = new();
}
