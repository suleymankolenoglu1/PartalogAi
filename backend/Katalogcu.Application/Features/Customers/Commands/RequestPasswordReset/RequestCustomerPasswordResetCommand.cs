using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;

public sealed record RequestCustomerPasswordResetCommand(
    Guid OwnerUserId,
    string? Phone,
    string? Email)
    : IRequest<OperationResult<RequestCustomerPasswordResetResponse>>;
