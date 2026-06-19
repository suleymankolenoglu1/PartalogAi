using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMachines;

public sealed record GetPublicCustomerMachinesQuery(Guid OwnerUserId, string SessionToken)
    : IRequest<OperationResult<IReadOnlyList<PublicCustomerMachineDto>>>;
