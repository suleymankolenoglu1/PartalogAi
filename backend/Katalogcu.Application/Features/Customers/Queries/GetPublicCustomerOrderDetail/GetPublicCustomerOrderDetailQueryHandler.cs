using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrderDetail;

public sealed class GetPublicCustomerOrderDetailQueryHandler : IRequestHandler<GetPublicCustomerOrderDetailQuery, OperationResult<PublicCustomerOrderDetailDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetPublicCustomerOrderDetailQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerOrderDetailDto>> Handle(GetPublicCustomerOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<PublicCustomerOrderDetailDto>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var order = await _customerRepository.GetOrderDetailByCustomerAsync(request.OrderId, customer.Id, cancellationToken);
        if (order == null)
        {
            return OperationResult<PublicCustomerOrderDetailDto>.Failure("not_found", "Sipariş bulunamadı.");
        }

        var dto = new PublicCustomerOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = (int)order.Status,
            TotalAmount = order.TotalAmount,
            CreatedDate = order.CreatedDate,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryCity = order.DeliveryCity,
            DeliveryDistrict = order.DeliveryDistrict,
            DeliveryNote = order.DeliveryNote,
            PaymentMethod = order.PaymentMethod,
            Items = order.Items.Select(i => new PublicCustomerOrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.UnitPrice * i.Quantity,
                Product = i.Product == null
                    ? null
                    : new PublicCustomerOrderItemProductDto
                    {
                        Id = i.Product.Id,
                        Code = i.Product.Code,
                        Name = i.Product.Name,
                        ImageUrl = i.Product.ImageUrl,
                        Description = i.Product.Description
                    }
            }).ToList(),
            StatusHistory = order.StatusHistory
                .Where(h => h.IsVisibleToCustomer)
                .OrderByDescending(h => h.CreatedDate)
                .Select(h => new PublicCustomerOrderStatusHistoryDto
                {
                    Id = h.Id,
                    PreviousStatus = h.PreviousStatus,
                    NewStatus = h.NewStatus,
                    Source = h.Source,
                    Note = h.Note,
                    ChangedBy = h.ChangedBy,
                    CreatedDate = h.CreatedDate
                })
                .ToList()
        };

        return OperationResult<PublicCustomerOrderDetailDto>.Success(dto);
    }
}
