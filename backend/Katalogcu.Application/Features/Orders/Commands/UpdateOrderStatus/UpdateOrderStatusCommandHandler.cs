using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, OperationResult<Order>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOrderRepository _orderRepository;

    public UpdateOrderStatusCommandHandler(ICurrentUserService currentUser, IOrderRepository orderRepository)
    {
        _currentUser = currentUser;
        _orderRepository = orderRepository;
    }

    public async Task<OperationResult<Order>> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<Order>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var order = await _orderRepository.GetOrderByIdForUserAsync(request.OrderId, _currentUser.UserId, cancellationToken);
        if (order == null)
        {
            return OperationResult<Order>.Failure("not_found", "Sipariş bulunamadı veya yetkiniz yok.");
        }

        var oldStatus = order.Status;
        var trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (oldStatus == request.Status && string.IsNullOrWhiteSpace(trimmedNote))
        {
            return OperationResult<Order>.Success(order);
        }

        order.Status = request.Status;
        order.UpdatedDate = DateTime.UtcNow;
        var statusEvent = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreviousStatus = (int)oldStatus,
            NewStatus = (int)request.Status,
            IsVisibleToCustomer = request.IsVisibleToCustomer,
            Source = "AdminUpdate",
            ChangedBy = _currentUser.ActorName,
            Note = trimmedNote,
            CreatedDate = DateTime.UtcNow
        };
        order.StatusHistory.Add(statusEvent);

        await _orderRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<Order>.Success(order);
    }
}
