using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.GetIncomingOrders;

public sealed record GetIncomingOrdersQuery : IRequest<OperationResult<IReadOnlyList<Order>>>;
