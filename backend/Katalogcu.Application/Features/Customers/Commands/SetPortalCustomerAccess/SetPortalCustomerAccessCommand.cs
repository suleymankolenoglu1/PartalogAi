using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.SetPortalCustomerAccess;

public sealed record SetPortalCustomerAccessCommand(
    Guid OwnerUserId,
    Guid CustomerId,
    bool IsActive)
    : IRequest<OperationResult<CustomerListItemDto>>;
