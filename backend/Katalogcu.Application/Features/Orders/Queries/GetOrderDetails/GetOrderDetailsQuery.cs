using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.GetOrderDetails;

public sealed record GetOrderDetailsQuery(Guid OrderId) : IRequest<OperationResult<Order>>;
