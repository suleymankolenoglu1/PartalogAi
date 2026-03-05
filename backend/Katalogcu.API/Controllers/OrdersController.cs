using Microsoft.AspNetCore.Mvc;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Authorization; // Yetki için
using Katalogcu.Application.Features.Orders.Commands.CreateOrder;
using Katalogcu.Application.Features.Orders.Commands.UpdateOrderStatus;
using Katalogcu.Application.Features.Orders.Queries.GetIncomingOrders;
using Katalogcu.Application.Features.Orders.Queries.GetOrderDetails;
using Katalogcu.Application.Features.Catalogs.Queries.GetPublicStorefront;
using FluentValidation;
using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers
{
    // 🔥 Varsayılan olarak her şey kilitli (Admin Paneli İçin)
    [Authorize(Policy = "PrivilegedUser")] 
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IPublicAccessTokenService _publicAccessTokenService;
        private readonly ISender _sender;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IPublicAccessTokenService publicAccessTokenService,
            ISender sender,
            ILogger<OrdersController> logger)
        {
            _publicAccessTokenService = publicAccessTokenService;
            _sender = sender;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idString, out var userId) ? userId : Guid.Empty;
        }

        // ============================================================
        // 🟢 PUBLIC (HALKA AÇIK) ENDPOINTLER
        // ============================================================

        // 1. SİPARİŞ OLUŞTUR (Vitrinden gelir, Login gerektirmez)
        [AllowAnonymous] 
        [EnableRateLimiting("public-order")]
        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader = null)
        {
            var authenticatedUserId = GetCurrentUserId();
            var publicPayload = !string.IsNullOrWhiteSpace(request.PublicToken)
                ? _publicAccessTokenService.Validate(request.PublicToken)
                : null;

            if (authenticatedUserId == Guid.Empty && publicPayload == null)
            {
                return BadRequest("Geçerli kullanıcı veya public token gerekli.");
            }

            if (authenticatedUserId != Guid.Empty &&
                publicPayload != null &&
                publicPayload.UserId != authenticatedUserId)
            {
                return Forbid();
            }

            var orderOwnerUserId = publicPayload?.UserId ?? authenticatedUserId;
            if (orderOwnerUserId != Guid.Empty)
            {
                var storefrontResult = await _sender.Send(
                    new GetPublicStorefrontQuery(orderOwnerUserId),
                    HttpContext.RequestAborted);

                if (!storefrontResult.IsSuccess)
                {
                    return storefrontResult.ErrorCode switch
                    {
                        "not_found" => NotFound(storefrontResult.ErrorMessage),
                        "validation" => BadRequest(storefrontResult.ErrorMessage),
                        _ => StatusCode(500, storefrontResult.ErrorMessage ?? "Plan bilgisi alınamadı.")
                    };
                }

                if (storefrontResult.Value?.EcommerceEnabled != true)
                {
                    return StatusCode(403, new
                    {
                        message = "Bu işletmede e-ticaret özelliği aktif değil."
                    });
                }
            }

            try
            {
                var command = new CreateOrderCommand(
                    string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? request.IdempotencyKey : idempotencyKeyHeader,
                    authenticatedUserId == Guid.Empty ? null : authenticatedUserId,
                    request.CustomerName,
                    request.CustomerEmail,
                    request.CustomerPhone,
                    request.DeliveryAddress,
                    request.DeliveryCity,
                    request.DeliveryDistrict,
                    request.DeliveryNote,
                    request.PaymentMethod,
                    publicPayload?.UserId,
                    request.PublicSessionToken,
                    publicPayload?.CatalogIds,
                    (request.Items ?? []).Select(i => new CreateOrderItemInput
                    {
                        ProductId = i.ProductId,
                        PartCode = i.PartCode,
                        Quantity = i.Quantity,
                        Price = i.Price
                    }).ToList());

                var result = await _sender.Send(command);
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Sipariş oluşturulamadı.")
                    };
                }
                
                var response = result.Value!;

                return Ok(new 
                { 
                    message = response.IsIdempotentReplay ? "Sipariş zaten alınmıştı (idempotent replay)." : "Sipariş başarıyla alındı.", 
                    orderId = response.OrderId,
                    orderNumber = response.OrderNumber,
                    idempotentReplay = response.IsIdempotentReplay
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Public order create failed");
                return StatusCode(500, "Sipariş oluşturma sırasında beklenmeyen bir hata oluştu.");
            }
        }

        // ============================================================
        // 🔒 ADMIN (YETKİLİ) ENDPOINTLER
        // ============================================================

        // 2. GELEN SİPARİŞLERİ LİSTELE (Sadece Benim Ürünlerim)
        [HttpGet]
        public async Task<IActionResult> GetIncomingOrders()
        {
            try
            {
                var result = await _sender.Send(new GetIncomingOrdersQuery());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Siparişler alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 3. SİPARİŞ DETAYI
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            try
            {
                var result = await _sender.Send(new GetOrderDetailsQuery(id));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "forbidden" => Unauthorized(result.ErrorMessage),
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Sipariş detayı alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 4. SİPARİŞ DURUMU GÜNCELLE
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto request)
        {
            try
            {
                var result = await _sender.Send(new UpdateOrderStatusCommand(id, request.Status, request.Note, request.IsVisibleToCustomer));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Sipariş durumu güncellenemedi.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    // --- DTO'lar ---
    public class CreateOrderRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryCity { get; set; } = string.Empty;
        public string? DeliveryDistrict { get; set; }
        public string? DeliveryNote { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PublicToken { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? PublicSessionToken { get; set; }
        public List<CartItemDto> Items { get; set; } = [];
    }

    public class CartItemDto
    {
        public Guid ProductId { get; set; }
        public string? PartCode { get; set; }
        public string? PartName { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
    }

    public class UpdateStatusDto 
    {
        public OrderStatus Status { get; set; }
        public string? Note { get; set; }
        public bool IsVisibleToCustomer { get; set; } = true;
    }
}
