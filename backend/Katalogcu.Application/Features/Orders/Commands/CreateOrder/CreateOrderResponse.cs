namespace Katalogcu.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderResponse
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public bool IsIdempotentReplay { get; init; }
}
