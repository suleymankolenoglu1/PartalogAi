using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.ConfirmPasswordReset;

public sealed record ConfirmCustomerPasswordResetCommand(
    Guid OwnerUserId,
    string? Phone,
    string? Email,
    string ResetCode,
    string NewPassword)
    : IRequest<OperationResult<PublicCustomerAuthResponse>>;
