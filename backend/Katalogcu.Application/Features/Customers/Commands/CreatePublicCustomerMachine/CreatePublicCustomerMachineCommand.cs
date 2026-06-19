using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.CreatePublicCustomerMachine;

public sealed record CreatePublicCustomerMachineCommand(
    Guid OwnerUserId,
    string SessionToken,
    string Brand,
    string Model,
    string? Variant,
    string? MachineGroup,
    string? SerialNumber,
    string? DisplayName,
    bool MakeActive)
    : IRequest<OperationResult<PublicCustomerMachineDto>>;
