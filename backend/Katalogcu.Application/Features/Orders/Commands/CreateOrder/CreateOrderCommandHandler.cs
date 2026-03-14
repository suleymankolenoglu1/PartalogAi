using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OperationResult<CreateOrderResponse>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IErpGatewayService _erpGatewayService;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IErpGatewayService erpGatewayService)
    {
        _orderRepository = orderRepository;
        _erpGatewayService = erpGatewayService;
    }

    public async Task<OperationResult<CreateOrderResponse>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var scopeUserId = request.PublicUserId ?? request.AuthenticatedUserId;
        if (!scopeUserId.HasValue || scopeUserId.Value == Guid.Empty)
        {
            return OperationResult<CreateOrderResponse>.Failure("validation", "Sipariş kapsamı belirlenemedi.");
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingOrder = await _orderRepository.GetOrderByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existingOrder != null)
            {
                if (existingOrder.OwnerUserId.HasValue && existingOrder.OwnerUserId.Value != scopeUserId.Value)
                {
                    return OperationResult<CreateOrderResponse>.Failure("forbidden", "Idempotency anahtarı bu kullanıcı kapsamına ait değil.");
                }

                return SuccessFromOrder(existingOrder, isReplay: true);
            }
        }

        return await _orderRepository.ExecuteInTransactionAsync(async txToken =>
        {
            var orderOwnerUserId = Guid.Empty;
            var createdAt = DateTime.UtcNow;
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = GenerateOrderNumber(),
                IdempotencyKey = idempotencyKey,
                OwnerUserId = scopeUserId.Value,
                CustomerName = request.CustomerName.Trim(),
                CustomerEmail = request.CustomerEmail?.Trim() ?? string.Empty,
                CustomerPhone = request.CustomerPhone.Trim(),
                DeliveryAddress = request.DeliveryAddress.Trim(),
                DeliveryCity = request.DeliveryCity.Trim(),
                DeliveryDistrict = string.IsNullOrWhiteSpace(request.DeliveryDistrict) ? null : request.DeliveryDistrict.Trim(),
                DeliveryNote = string.IsNullOrWhiteSpace(request.DeliveryNote) ? null : request.DeliveryNote.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "KapidaOdeme" : request.PaymentMethod.Trim(),
                CreatedDate = createdAt,
                Status = OrderStatus.Pending,
                Items = new List<OrderItem>()
            };

            order.StatusHistory.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PreviousStatus = null,
                NewStatus = (int)OrderStatus.Pending,
                IsVisibleToCustomer = true,
                Source = "OrderCreated",
                ChangedBy = request.PublicUserId.HasValue && request.PublicUserId.Value != Guid.Empty
                    ? "public-customer"
                    : "admin-user",
                Note = "Sipariş oluşturuldu.",
                CreatedDate = createdAt
            });

            decimal calculatedTotalAmount = 0;
            foreach (var itemInput in request.Items)
            {
                Product? product = null;
                if (itemInput.ProductId != Guid.Empty)
                {
                    product = await _orderRepository.GetProductByIdWithCatalogAsync(itemInput.ProductId, txToken);
                }

                if (product == null &&
                    request.PublicUserId.HasValue &&
                    request.PublicUserId.Value != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(itemInput.PartCode))
                {
                    product = await _orderRepository.GetLatestProductByCodeAsync(
                        itemInput.PartCode.Trim(),
                        request.PublicUserId,
                        request.PublicCatalogIds,
                        txToken);
                }

                if (product == null)
                {
                    continue;
                }

                if (!IsProductAccessible(product, request))
                {
                    return OperationResult<CreateOrderResponse>.Failure(
                        "forbidden",
                        "Sepette erişim izni olmayan ürün bulundu.");
                }

                var productOwnerUserId = product.Catalog?.UserId ?? Guid.Empty;
                if (productOwnerUserId == Guid.Empty)
                {
                    return OperationResult<CreateOrderResponse>.Failure(
                        "validation",
                        "Ürün sahipliği doğrulanamadı.");
                }

                if (orderOwnerUserId == Guid.Empty)
                {
                    orderOwnerUserId = productOwnerUserId;
                }
                else if (orderOwnerUserId != productOwnerUserId)
                {
                    return OperationResult<CreateOrderResponse>.Failure(
                        "validation",
                        "Sepette farklı işletmelere ait ürünler var.");
                }

                var quantity = itemInput.Quantity > 0 ? itemInput.Quantity : 1;
                var erpAvailability = await _erpGatewayService.GetProductAvailabilityAsync(
                    new ErpProductAvailabilityRequest
                    {
                        OwnerUserId = productOwnerUserId,
                        ProductId = product.Id,
                        PartCode = product.Code,
                        RequestedQuantity = quantity
                    },
                    txToken);

                if (erpAvailability == null || !erpAvailability.UnitPrice.HasValue)
                {
                    return OperationResult<CreateOrderResponse>.Failure(
                        "erp_unavailable",
                        $"ERP fiyat bilgisi alınamadı: {product.Code}");
                }

                if (!erpAvailability.IsAvailable)
                {
                    return OperationResult<CreateOrderResponse>.Failure(
                        "out_of_stock",
                        $"Yetersiz stok: {product.Code}");
                }

                calculatedTotalAmount += erpAvailability.UnitPrice.Value * quantity;

                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = erpAvailability.UnitPrice.Value
                });
            }

            if (order.Items.Count == 0)
            {
                order.TotalAmount = request.Items.Sum(i => (i.Price ?? 0m) * (i.Quantity > 0 ? i.Quantity : 1));
            }
            else
            {
                order.TotalAmount = calculatedTotalAmount;
            }

            var sessionCustomer = await ResolveCustomerFromPublicSessionAsync(request, txToken);
            if (sessionCustomer != null)
            {
                sessionCustomer.FullName = string.IsNullOrWhiteSpace(request.CustomerName) ? sessionCustomer.FullName : request.CustomerName.Trim();
                sessionCustomer.Phone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? sessionCustomer.Phone : request.CustomerPhone.Trim();
                sessionCustomer.NormalizedPhone = string.IsNullOrWhiteSpace(NormalizePhone(request.CustomerPhone))
                    ? sessionCustomer.NormalizedPhone
                    : NormalizePhone(request.CustomerPhone);
                sessionCustomer.Email = string.IsNullOrWhiteSpace(request.CustomerEmail) ? sessionCustomer.Email : request.CustomerEmail.Trim().ToLowerInvariant();
                ApplyOrderToCustomer(sessionCustomer, order);
                order.CustomerId = sessionCustomer.Id;
            }
            else
            {
                var ownerHint = request.PublicUserId ?? request.AuthenticatedUserId;
                var customer = await UpsertCustomerFromOrderAsync(order, ownerHint, txToken);
                order.CustomerId = customer?.Id;
            }

            await _orderRepository.AddOrderAsync(order, txToken);
            try
            {
                await _orderRepository.SaveChangesAsync(txToken);
            }
            catch when (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existingAfterConflict = await _orderRepository.GetOrderByIdempotencyKeyAsync(idempotencyKey, txToken);
                if (existingAfterConflict != null)
                {
                    return SuccessFromOrder(existingAfterConflict, isReplay: true);
                }

                throw;
            }

            return SuccessFromOrder(order, isReplay: false);
        }, cancellationToken);
    }

    private async Task<Customer?> UpsertCustomerFromOrderAsync(Order order, Guid? ownerUserIdHint, CancellationToken cancellationToken)
    {
        var productIds = order.Items
            .Select(i => i.ProductId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var ownerUserId = await _orderRepository.ResolveOwnerUserIdAsync(productIds, ownerUserIdHint, cancellationToken);
        if (ownerUserId == Guid.Empty) return null;

        var normalizedPhone = NormalizePhone(order.CustomerPhone);
        var normalizedEmail = string.IsNullOrWhiteSpace(order.CustomerEmail)
            ? null
            : order.CustomerEmail.Trim().ToLowerInvariant();

        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            customer = await _orderRepository.GetCustomerByPhoneAsync(ownerUserId, normalizedPhone, cancellationToken);
        }

        if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            customer = await _orderRepository.GetCustomerByEmailAsync(ownerUserId, normalizedEmail, cancellationToken);
        }

        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                CreatedDate = DateTime.UtcNow
            };
            await _orderRepository.AddCustomerAsync(customer, cancellationToken);
        }

        customer.FullName = string.IsNullOrWhiteSpace(order.CustomerName) ? customer.FullName : order.CustomerName.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(order.CustomerPhone) ? customer.Phone : order.CustomerPhone.Trim();
        customer.NormalizedPhone = string.IsNullOrWhiteSpace(normalizedPhone) ? customer.NormalizedPhone : normalizedPhone;
        customer.Email = string.IsNullOrWhiteSpace(normalizedEmail) ? customer.Email : normalizedEmail;
        customer.CompanyName = string.IsNullOrWhiteSpace(order.CompanyName) ? customer.CompanyName : order.CompanyName.Trim();
        customer.IsActive = true;
        customer.LastVisitDate = DateTime.UtcNow;
        customer.LastOrderDate = order.CreatedDate;
        customer.OrderCount += 1;
        customer.TotalSpent += order.TotalAmount;
        customer.UpdatedDate = DateTime.UtcNow;
        return customer;
    }

    private async Task<Customer?> ResolveCustomerFromPublicSessionAsync(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (!request.PublicUserId.HasValue || request.PublicUserId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.PublicSessionToken))
            return null;

        return await _orderRepository.GetCustomerByPublicSessionAsync(
            request.PublicUserId.Value,
            request.PublicSessionToken,
            DateTime.UtcNow,
            cancellationToken);
    }

    private static void ApplyOrderToCustomer(Customer customer, Order order)
    {
        customer.LastVisitDate = DateTime.UtcNow;
        customer.LastOrderDate = order.CreatedDate;
        customer.OrderCount += 1;
        customer.TotalSpent += order.TotalAmount;
        customer.UpdatedDate = DateTime.UtcNow;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static string GenerateOrderNumber()
    {
        return $"SP-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return idempotencyKey.Trim();
    }

    private static OperationResult<CreateOrderResponse> SuccessFromOrder(Order order, bool isReplay)
    {
        return OperationResult<CreateOrderResponse>.Success(new CreateOrderResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            IsIdempotentReplay = isReplay
        });
    }

    private static bool IsProductAccessible(Product product, CreateOrderCommand request)
    {
        var productCatalog = product.Catalog;
        if (productCatalog == null)
        {
            return false;
        }

        if (request.PublicUserId.HasValue && request.PublicUserId.Value != Guid.Empty)
        {
            if (productCatalog.UserId != request.PublicUserId.Value)
            {
                return false;
            }

            if (!string.Equals(productCatalog.Status, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (request.PublicCatalogIds is { Count: > 0 } && !request.PublicCatalogIds.Contains(productCatalog.Id))
            {
                return false;
            }

            return true;
        }

        if (request.AuthenticatedUserId.HasValue && request.AuthenticatedUserId.Value != Guid.Empty)
        {
            return productCatalog.UserId == request.AuthenticatedUserId.Value;
        }

        return false;
    }
}
