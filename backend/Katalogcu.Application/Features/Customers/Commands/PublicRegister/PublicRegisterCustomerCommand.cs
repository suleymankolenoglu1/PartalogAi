using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegister;

public sealed record PublicRegisterCustomerCommand(
    Guid OwnerUserId,
    string Name,
    string Phone,
    string? Email,
    string? CompanyName,
    string? Note)
    : IRequest<OperationResult<PublicRegisterCustomerResponse>>;
