using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMe;

public sealed record GetPublicCustomerMeQuery(Guid OwnerUserId, string SessionToken)
    : IRequest<OperationResult<PublicCustomerDto>>;
