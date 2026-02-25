using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicLogin;

public sealed record PublicCustomerLoginCommand(
    Guid OwnerUserId,
    string Phone,
    string? Email,
    string Password)
    : IRequest<OperationResult<PublicCustomerAuthResponse>>;
