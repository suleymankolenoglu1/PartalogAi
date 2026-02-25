using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.GetIncomingOrders;

public sealed class GetIncomingOrdersQueryHandler : IRequestHandler<GetIncomingOrdersQuery, OperationResult<IReadOnlyList<Order>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOrderRepository _orderRepository;

    public GetIncomingOrdersQueryHandler(ICurrentUserService currentUser, IOrderRepository orderRepository)
    {
        _currentUser = currentUser;
        _orderRepository = orderRepository;
    }

    public async Task<OperationResult<IReadOnlyList<Order>>> Handle(GetIncomingOrdersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<Order>>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var orders = await _orderRepository.GetIncomingOrdersForUserAsync(_currentUser.UserId, cancellationToken);
        return OperationResult<IReadOnlyList<Order>>.Success(orders);
    }
}
