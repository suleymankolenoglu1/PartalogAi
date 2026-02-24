using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Authorization; // Yetki için
using System.Security.Claims; // User ID için

namespace Katalogcu.API.Controllers
{
    // 🔥 Varsayılan olarak her şey kilitli (Admin Paneli İçin)
    [Authorize] 
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublicLinkService _publicLinkService;

        public OrdersController(AppDbContext context, IPublicLinkService publicLinkService)
        {
            _context = context;
            _publicLinkService = publicLinkService;
        }

        // 🛠️ Helper: Token'dan Admin UserID'sini okur
        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            return new string(phone.Where(char.IsDigit).ToArray());
        }

        private async Task<Customer?> UpsertCustomerFromOrderAsync(Order order, Guid? ownerUserIdHint = null)
        {
            var productIds = order.Items
                .Select(i => i.ProductId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            Guid ownerUserId;
            if (productIds.Any())
            {
                ownerUserId = await (
                    from p in _context.Products.AsNoTracking()
                    join c in _context.Catalogs.AsNoTracking() on p.CatalogId equals c.Id
                    where productIds.Contains(p.Id)
                    select c.UserId
                ).FirstOrDefaultAsync();
            }
            else
            {
                ownerUserId = ownerUserIdHint ?? Guid.Empty;
            }

            if (ownerUserId == Guid.Empty) return null;

            var normalizedPhone = NormalizePhone(order.CustomerPhone);
            var normalizedEmail = string.IsNullOrWhiteSpace(order.CustomerEmail)
                ? null
                : order.CustomerEmail.Trim().ToLowerInvariant();

            Customer? customer = null;

            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == ownerUserId &&
                    c.NormalizedPhone == normalizedPhone);
            }

            if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == ownerUserId &&
                    c.Email != null &&
                    c.Email.ToLower() == normalizedEmail);
            }

            if (customer == null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    UserId = ownerUserId,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
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

        private async Task<Customer?> ResolveCustomerFromPublicSessionAsync(CreateOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken) || string.IsNullOrWhiteSpace(request.PublicSessionToken))
                return null;

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null) return null;

            var now = DateTime.UtcNow;
            var customer = await _context.Customers.FirstOrDefaultAsync(c =>
                c.UserId == payload.UserId &&
                c.PublicSessionToken == request.PublicSessionToken &&
                c.PublicSessionExpiresAt != null &&
                c.PublicSessionExpiresAt > now);

            return customer;
        }

        private static void ApplyOrderToCustomer(Customer customer, Order order)
        {
            customer.LastVisitDate = DateTime.UtcNow;
            customer.LastOrderDate = order.CreatedDate;
            customer.OrderCount += 1;
            customer.TotalSpent += order.TotalAmount;
            customer.UpdatedDate = DateTime.UtcNow;
        }

        // ============================================================
        // 🟢 PUBLIC (HALKA AÇIK) ENDPOINTLER
        // ============================================================

        // 1. SİPARİŞ OLUŞTUR (Vitrinden gelir, Login gerektirmez)
        [AllowAnonymous] 
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            // --- Validasyonlar ---
            if (request.Items == null || !request.Items.Any())
                return BadRequest("Sepet boş, sipariş oluşturulamaz.");

            if (string.IsNullOrEmpty(request.CustomerName) || string.IsNullOrEmpty(request.CustomerPhone))
                return BadRequest("Müşteri adı ve telefon numarası zorunludur.");
            if (string.IsNullOrWhiteSpace(request.DeliveryAddress) || string.IsNullOrWhiteSpace(request.DeliveryCity))
                return BadRequest("Teslimat adresi ve şehir zorunludur.");

            // --- Sipariş Nesnesi ---
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"SP-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                DeliveryAddress = request.DeliveryAddress.Trim(),
                DeliveryCity = request.DeliveryCity.Trim(),
                DeliveryDistrict = string.IsNullOrWhiteSpace(request.DeliveryDistrict) ? null : request.DeliveryDistrict.Trim(),
                DeliveryNote = string.IsNullOrWhiteSpace(request.DeliveryNote) ? null : request.DeliveryNote.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "KapidaOdeme" : request.PaymentMethod.Trim(),
                
                CreatedDate = DateTime.UtcNow,
                Status = OrderStatus.Pending, // Varsayılan: Bekliyor
                Items = new List<OrderItem>()
            };

            PublicLinkPayload? publicPayload = null;
            if (!string.IsNullOrWhiteSpace(request.PublicToken))
            {
                publicPayload = _publicLinkService.Validate(request.PublicToken);
            }

            decimal calculatedTotalAmount = 0;

            // --- Kalemleri İşle ---
            foreach (var itemDto in request.Items)
            {
                // Fiyatı DB'den çek (Güvenlik)
                Product? product = null;
                if (itemDto.ProductId != Guid.Empty)
                {
                    product = await _context.Products
                        .Include(p => p.Catalog)
                        .FirstOrDefaultAsync(p => p.Id == itemDto.ProductId);
                }

                if (product == null && !string.IsNullOrWhiteSpace(itemDto.PartCode))
                {
                    var code = itemDto.PartCode.Trim();
                    var query = _context.Products
                        .Include(p => p.Catalog)
                        .Where(p => p.Code == code);

                    if (publicPayload != null)
                    {
                        query = query.Where(p => p.Catalog.UserId == publicPayload.UserId && p.Catalog.Status == "Published");
                    }

                    product = await query.OrderByDescending(p => p.CreatedDate).FirstOrDefaultAsync();
                }

                if (product == null) continue; // Ürün silinmişse atla

                var quantity = itemDto.Quantity > 0 ? itemDto.Quantity : 1;
                var lineTotal = product.Price * quantity;
                calculatedTotalAmount += lineTotal;

                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price 
                });
            }

            if (!order.Items.Any())
            {
                // Stok yönetimi henüz aktif değilse de sipariş test edilebilsin.
                order.TotalAmount = request.Items.Sum(i => (i.Price ?? 0m) * (i.Quantity > 0 ? i.Quantity : 1));
            }
            else
            {
                order.TotalAmount = calculatedTotalAmount;
            }

            // --- Kaydet ---
            try
            {
                _context.Orders.Add(order);
                var sessionCustomer = await ResolveCustomerFromPublicSessionAsync(request);
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
                    var customer = await UpsertCustomerFromOrderAsync(order, publicPayload?.UserId);
                    order.CustomerId = customer?.Id;
                }

                await _context.SaveChangesAsync();

                return Ok(new 
                { 
                    message = "Sipariş başarıyla alındı.", 
                    orderId = order.Id, 
                    orderNumber = order.OrderNumber 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        // ============================================================
        // 🔒 ADMIN (YETKİLİ) ENDPOINTLER
        // ============================================================

        // 2. GELEN SİPARİŞLERİ LİSTELE (Sadece Benim Ürünlerim)
        [HttpGet]
        public async Task<IActionResult> GetIncomingOrders()
        {
            var userId = GetCurrentUserId();

            // 🔥 SORGUNUN MANTIĞI:
            // Bir siparişi, eğer içindeki ürünlerden EN AZ BİRİ benim kataloğuma aitse getir.
            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Catalog)
                .Where(o =>
                    o.Items.Any(i => i.Product.Catalog.UserId == userId) ||
                    (o.CustomerId != null && _context.Customers.Any(c => c.Id == o.CustomerId && c.UserId == userId))) // 🔒 İzolasyon
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            return Ok(orders);
        }

        // 3. SİPARİŞ DETAYI
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            var userId = GetCurrentUserId();

            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Catalog)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Güvenlik: Bu siparişteki ürünlerin sahibi ben miyim?
            var belongsToMe = order.Items.Any(i => i.Product?.Catalog?.UserId == userId);
            if (!belongsToMe && order.CustomerId != null)
            {
                belongsToMe = await _context.Customers.AnyAsync(c => c.Id == order.CustomerId && c.UserId == userId);
            }
            
            if (!belongsToMe) return Unauthorized("Bu siparişi görüntüleme yetkiniz yok.");

            return Ok(order);
        }

        // 4. SİPARİŞ DURUMU GÜNCELLE
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto request)
        {
             var userId = GetCurrentUserId();
             
             var order = await _context.Orders
                 .Include(o => o.Items)
                 .ThenInclude(i => i.Product)
                 .ThenInclude(p => p.Catalog)
                 .FirstOrDefaultAsync(o =>
                     o.Id == id &&
                     (o.Items.Any(i => i.Product.Catalog.UserId == userId) ||
                      (o.CustomerId != null && _context.Customers.Any(c => c.Id == o.CustomerId && c.UserId == userId))));

             if (order == null) return NotFound("Sipariş bulunamadı veya yetkiniz yok.");

             // Status enum ise parse et, string ise direkt ata
             // Burada basitlik için OrderStatus enum kullandığını varsayıyorum
             order.Status = request.Status; 
             
             await _context.SaveChangesAsync();
             return Ok(order);
        }
    }

    // --- DTO'lar ---
    public class CreateOrderRequest
    {
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryCity { get; set; } = string.Empty;
        public string? DeliveryDistrict { get; set; }
        public string? DeliveryNote { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PublicToken { get; set; }
        public string? PublicSessionToken { get; set; }
        public List<CartItemDto> Items { get; set; }
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
    }
}
