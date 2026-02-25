using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrders;

public sealed class GetPublicCustomerOrdersQueryHandler : IRequestHandler<GetPublicCustomerOrdersQuery, OperationResult<IReadOnlyList<PublicCustomerOrderSummaryDto>>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetPublicCustomerOrdersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<IReadOnlyList<PublicCustomerOrderSummaryDto>>> Handle(GetPublicCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<IReadOnlyList<PublicCustomerOrderSummaryDto>>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var orders = await _customerRepository.GetOrdersByCustomerIdAsync(customer.Id, cancellationToken);
        var result = orders.Select(o => new PublicCustomerOrderSummaryDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = (int)o.Status,
            TotalAmount = o.TotalAmount,
            CreatedDate = o.CreatedDate,
            PaymentMethod = o.PaymentMethod,
            DeliveryCity = o.DeliveryCity,
            ItemCount = o.Items.Count
        }).ToList();

        return OperationResult<IReadOnlyList<PublicCustomerOrderSummaryDto>>.Success(result);
    }
}
