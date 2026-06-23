using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.UpsertPortalCustomer;

public sealed record UpsertPortalCustomerCommand(
    Guid OwnerUserId,
    Guid? CustomerId,
    string Name,
    string Phone,
    string? Email,
    string? CompanyName,
    string? Note,
    string? InitialPassword,
    bool IsActive)
    : IRequest<OperationResult<CustomerListItemDto>>;
