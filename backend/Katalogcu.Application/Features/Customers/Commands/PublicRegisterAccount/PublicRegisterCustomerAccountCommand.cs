using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;

public sealed record PublicRegisterCustomerAccountCommand(
    Guid OwnerUserId,
    string Name,
    string Phone,
    string? Email,
    string Password)
    : IRequest<OperationResult<PublicCustomerAuthResponse>>;
