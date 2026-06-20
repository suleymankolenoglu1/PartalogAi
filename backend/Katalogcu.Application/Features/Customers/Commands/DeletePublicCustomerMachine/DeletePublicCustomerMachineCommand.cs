using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.DeletePublicCustomerMachine;

public sealed record DeletePublicCustomerMachineCommand(Guid OwnerUserId, string SessionToken, Guid MachineId)
    : IRequest<OperationResult<bool>>;
