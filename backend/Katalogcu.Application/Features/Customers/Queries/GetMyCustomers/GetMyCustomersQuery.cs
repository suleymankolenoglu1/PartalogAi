using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetMyCustomers;

public sealed record GetMyCustomersQuery : IRequest<OperationResult<IReadOnlyList<CustomerListItemDto>>>;
