using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrderDetail;

public sealed record GetPublicCustomerOrderDetailQuery(
    Guid OwnerUserId,
    string SessionToken,
    Guid OrderId)
    : IRequest<OperationResult<PublicCustomerOrderDetailDto>>;
