using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrders;

public sealed record GetPublicCustomerOrdersQuery(Guid OwnerUserId, string SessionToken)
    : IRequest<OperationResult<IReadOnlyList<PublicCustomerOrderSummaryDto>>>;
