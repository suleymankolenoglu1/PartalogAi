using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.UpdatePublicCustomerMachine;

public sealed record UpdatePublicCustomerMachineCommand(
    Guid OwnerUserId,
    string SessionToken,
    Guid MachineId,
    string Brand,
    string Model,
    string? Variant,
    string? MachineGroup,
    string? SerialNumber,
    string? DisplayName,
    bool IsActive)
    : IRequest<OperationResult<PublicCustomerMachineDto>>;
