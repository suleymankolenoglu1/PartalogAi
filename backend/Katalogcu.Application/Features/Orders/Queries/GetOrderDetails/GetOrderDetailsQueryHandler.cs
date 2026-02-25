using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.GetOrderDetails;

public sealed class GetOrderDetailsQueryHandler : IRequestHandler<GetOrderDetailsQuery, OperationResult<Order>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOrderRepository _orderRepository;

    public GetOrderDetailsQueryHandler(ICurrentUserService currentUser, IOrderRepository orderRepository)
    {
        _currentUser = currentUser;
        _orderRepository = orderRepository;
    }

    public async Task<OperationResult<Order>> Handle(GetOrderDetailsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<Order>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var order = await _orderRepository.GetOrderByIdWithItemsAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            return OperationResult<Order>.Failure("not_found", "Sipariş bulunamadı.");
        }

        var belongsToUser = order.OwnerUserId == _currentUser.UserId ||
                            order.Items.Any(i => i.Product?.Catalog?.UserId == _currentUser.UserId);
        if (!belongsToUser && order.CustomerId.HasValue)
        {
            belongsToUser = await _orderRepository.IsCustomerOwnedByUserAsync(order.CustomerId.Value, _currentUser.UserId, cancellationToken);
        }

        if (!belongsToUser)
        {
            return OperationResult<Order>.Failure("forbidden", "Bu siparişi görüntüleme yetkiniz yok.");
        }

        return OperationResult<Order>.Success(order);
    }
}
