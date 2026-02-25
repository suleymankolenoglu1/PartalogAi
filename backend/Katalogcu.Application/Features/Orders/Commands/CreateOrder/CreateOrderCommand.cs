using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    string? IdempotencyKey,
    Guid? AuthenticatedUserId,
    string CustomerName,
    string? CustomerEmail,
    string CustomerPhone,
    string DeliveryAddress,
    string DeliveryCity,
    string? DeliveryDistrict,
    string? DeliveryNote,
    string? PaymentMethod,
    Guid? PublicUserId,
    string? PublicSessionToken,
    IReadOnlyCollection<Guid>? PublicCatalogIds,
    IReadOnlyList<CreateOrderItemInput> Items)
    : IRequest<OperationResult<CreateOrderResponse>>;

public sealed class CreateOrderItemInput
{
    public Guid ProductId { get; init; }
    public string? PartCode { get; init; }
    public int Quantity { get; init; }
    public decimal? Price { get; init; }
}
