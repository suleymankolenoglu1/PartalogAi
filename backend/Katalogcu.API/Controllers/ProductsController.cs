using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Katalogcu.API.Services;
using System.Security.Claims; // ✨ User ID okumak için

namespace Katalogcu.API.Controllers
{
    [Authorize] // 🔒 Sadece giriş yapanlar
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ExcelService _excelService;
        private readonly IPublicLinkService _publicLinkService;

        public ProductsController(AppDbContext context, ExcelService excelService, IPublicLinkService publicLinkService)
        {
            _context = context;
            _excelService = excelService;
            _publicLinkService = publicLinkService;
        }

        // 🛠️ Yardımcı Metod: Token'dan UserID'yi (Guid) okur
        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private string GetCurrentActorName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value
                   ?? User.FindFirst(ClaimTypes.Email)?.Value
                   ?? "admin";
        }

        private (Guid userId, bool isPublic, PublicLinkPayload? publicPayload) ResolveAccess(string? token)
        {
            var tokenUserId = GetCurrentUserId();
            if (tokenUserId != Guid.Empty) return (tokenUserId, false, null);

            if (!string.IsNullOrWhiteSpace(token))
            {
                var payload = _publicLinkService.Validate(token);
                if (payload != null) return (payload.UserId, true, payload);
            }

            return (Guid.Empty, true, null);
        }

        // 1. TÜM ÜRÜNLERİ GETİR (SADECE BENİM OLANLAR)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetCurrentUserId();

            // 🔥 DÜZELTME: Sadece giriş yapan kullanıcının kataloglarına bağlı ürünleri getir.
            var products = await _context.Products
                .Include(p => p.Catalog)
                .Where(p => p.Catalog.UserId == userId) // 🔒 Veri İzolasyonu
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new 
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.OemNo,
                    p.Price,
                    p.StockQuantity,
                    p.ImageUrl,
                    p.Category,
                    CatalogName = p.Catalog != null ? p.Catalog.Name : "Genel Stok",
                    CatalogId = p.CatalogId
                })
                .ToListAsync();

            return Ok(products);
        }

        // 2. KATALOĞA GÖRE GETİR (Vitrin için açık bırakıldı)
        [AllowAnonymous]
        [HttpGet("catalog/{catalogId}")]
        public async Task<IActionResult> GetByCatalog(Guid catalogId, [FromQuery] string? token)
        {
            var access = ResolveAccess(token);
            if (access.userId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            var query = _context.Products
                                .Include(p => p.Catalog)
                                .AsNoTracking()
                                .Where(p => p.CatalogId == catalogId && p.Catalog.UserId == access.userId);

            if (access.isPublic)
            {
                query = query.Where(p => p.Catalog.Status == "Published");
                if (access.publicPayload?.CatalogIds?.Any() == true)
                {
                    query = query.Where(p => access.publicPayload.CatalogIds.Contains(p.CatalogId));
                }
            }

            var products = await query.OrderBy(p => p.Code).ToListAsync();
            return Ok(products);
        }

        // 3. YENİ ÜRÜN EKLE
        [HttpPost]
        public async Task<IActionResult> Create(Product product)
        {
            var userId = GetCurrentUserId();

            // Güvenlik Kontrolü: Eklenmek istenen katalog bu kullanıcıya mı ait?
            if (product.CatalogId != Guid.Empty)
            {
                var ownsCatalog = await _context.Catalogs.AnyAsync(c => c.Id == product.CatalogId && c.UserId == userId);
                if (!ownsCatalog) return BadRequest("Seçilen katalog size ait değil veya bulunamadı.");
            }

            if (string.IsNullOrEmpty(product.Category)) product.Category = "Genel";

            product.CreatedDate = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return Ok(product);
        }
        
        // 4. ÜRÜN SİL (GÜÇLENDİRİLMİŞ)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();

            // Ürünü ve Kataloğunu bul
            var product = await _context.Products
                .Include(p => p.Catalog)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound("Ürün bulunamadı.");

            // 🔒 YETKİ KONTROLÜ: Ürün bir kataloğa bağlıysa, o katalog benim mi?
            if (product.Catalog != null && product.Catalog.UserId != userId)
            {
                return Unauthorized("Bu ürünü silme yetkiniz yok.");
            }

            try 
            {
                // A. Hotspotları Temizle
                var linkedHotspots = await _context.Hotspots.Where(h => h.ProductId == id).ToListAsync();
                if (linkedHotspots.Any())
                {
                    _context.Hotspots.RemoveRange(linkedHotspots);
                }

                // B. 🔥 SİPARİŞ KALEMLERİNİ TEMİZLE (FK Hatasını Önler)
                var orderItems = await _context.OrderItems.Where(oi => oi.ProductId == id).ToListAsync();
                if (orderItems.Any())
                {
                    _context.OrderItems.RemoveRange(orderItems);
                }

                // C. Ürünü Sil
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Silme hatası: {ex.Message}");
            }
        }

        // 5. EXCEL İLE TOPLU YÜKLEME
        [HttpPost("import")]
        public async Task<IActionResult> Import(IFormFile file, [FromForm] Guid? catalogId)
        {
            var userId = GetCurrentUserId();

            if (file == null || file.Length == 0)
                return BadRequest("Lütfen bir Excel dosyası yükleyin.");

            // 🔒 Güvenlik: Eğer bir kataloğa yükleme yapılıyorsa, katalog kullanıcının mı?
            if (catalogId.HasValue && catalogId != Guid.Empty)
            {
                var ownsCatalog = await _context.Catalogs.AnyAsync(c => c.Id == catalogId && c.UserId == userId);
                if (!ownsCatalog) return BadRequest("Seçilen katalog size ait değil.");
            }

            try 
            {
                var targetCatalogId = catalogId ?? Guid.Empty; 

                var products = _excelService.ParseProducts(file, targetCatalogId);

                if (products.Count == 0)
                    return BadRequest("Dosyada okunabilir ürün bulunamadı.");

                _context.Products.AddRange(products);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"{products.Count} adet ürün başarıyla yüklendi!", count = products.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Yükleme hatası: {ex.Message}");
            }
        }

        // 6. STOK İÇE AKTAR (CSV/XLSX)
        [HttpPost("import-stock")]
        public async Task<IActionResult> ImportStock(IFormFile file, [FromForm] Guid? catalogId, [FromForm] string mode = "update_only")
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized("Kullanıcı doğrulanamadı.");

            if (file == null || file.Length == 0)
                return BadRequest("Lütfen CSV veya Excel dosyası yükleyin.");

            // upsert modunda yeni ürün açılabilmesi için hedef katalog gerekir.
            var allowUpsert = string.Equals(mode, "upsert", StringComparison.OrdinalIgnoreCase);
            if (allowUpsert && (!catalogId.HasValue || catalogId.Value == Guid.Empty))
                return BadRequest("Upsert modunda yeni ürün oluşturmak için catalogId zorunludur.");

            if (catalogId.HasValue && catalogId.Value != Guid.Empty)
            {
                var ownsCatalog = await _context.Catalogs.AnyAsync(c => c.Id == catalogId && c.UserId == userId);
                if (!ownsCatalog) return BadRequest("Seçilen katalog size ait değil.");
            }

            try
            {
                var rows = _excelService.ParseStockRows(file);
                if (rows.Count == 0) return BadRequest("Dosyada işlenecek satır bulunamadı.");

                var query = _context.Products
                    .Include(p => p.Catalog)
                    .Where(p => p.Catalog != null && p.Catalog.UserId == userId);

                if (catalogId.HasValue && catalogId.Value != Guid.Empty)
                {
                    query = query.Where(p => p.CatalogId == catalogId.Value);
                }

                var existingProducts = await query.ToListAsync();
                var codeMap = existingProducts
                    .GroupBy(p => NormalizeCode(p.Code))
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToDictionary(g => g.Key, g => g.ToList());

                var updated = 0;
                var created = 0;
                var skipped = 0;
                var skippedRows = new List<StockImportSkipRow>();
                var movementLogs = new List<StockMovement>();
                var actorName = GetCurrentActorName();
                var importBatchId = Guid.NewGuid().ToString("N");

                foreach (var row in rows)
                {
                    var codeKey = NormalizeCode(row.Code);
                    if (string.IsNullOrWhiteSpace(codeKey))
                    {
                        skipped++;
                        skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Parça kodu boş."));
                        continue;
                    }

                    if (!row.StockQuantity.HasValue)
                    {
                        skipped++;
                        skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Stok adedi sayısal değil."));
                        continue;
                    }

                    if (row.StockQuantity.Value < 0)
                    {
                        skipped++;
                        skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Negatif stok desteklenmiyor."));
                        continue;
                    }

                    if (codeMap.TryGetValue(codeKey, out var matches))
                    {
                        if (matches.Count > 1)
                        {
                            skipped++;
                            skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Aynı koddan birden fazla ürün var. Katalog filtresiyle tekrar deneyin."));
                            continue;
                        }

                        var product = matches[0];
                        var previousQuantity = product.StockQuantity;
                        product.StockQuantity = row.StockQuantity.Value;
                        if (row.Price.HasValue) product.Price = row.Price.Value;
                        if (!string.IsNullOrWhiteSpace(row.Name)) product.Name = row.Name.Trim();
                        if (!string.IsNullOrWhiteSpace(row.Category)) product.Category = row.Category.Trim();
                        if (!string.IsNullOrWhiteSpace(row.Description)) product.Description = row.Description.Trim();
                        product.UpdatedDate = DateTime.UtcNow;

                        if (previousQuantity != product.StockQuantity)
                        {
                            movementLogs.Add(BuildStockMovement(
                                userId: userId,
                                product: product,
                                previousQuantity: previousQuantity,
                                newQuantity: product.StockQuantity,
                                movementType: "IMPORT",
                                reason: $"Stok import satırı #{row.RowNumber}",
                                source: "products/import-stock",
                                actorName: actorName,
                                referenceId: importBatchId));
                        }

                        updated++;
                        continue;
                    }

                    if (!allowUpsert)
                    {
                        skipped++;
                        skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Ürün bulunamadı (mode=update_only)."));
                        continue;
                    }

                    if (!catalogId.HasValue || catalogId.Value == Guid.Empty)
                    {
                        skipped++;
                        skippedRows.Add(new StockImportSkipRow(row.RowNumber, row.Code, "Yeni ürün için katalog seçilmedi."));
                        continue;
                    }

                    var newProduct = new Product
                    {
                        Id = Guid.NewGuid(),
                        CatalogId = catalogId.Value,
                        Code = row.Code.Trim(),
                        Name = !string.IsNullOrWhiteSpace(row.Name) ? row.Name.Trim() : $"Parça {row.Code.Trim()}",
                        Category = !string.IsNullOrWhiteSpace(row.Category) ? row.Category.Trim() : "Genel",
                        Description = row.Description?.Trim() ?? string.Empty,
                        Price = row.Price ?? 0,
                        StockQuantity = row.StockQuantity.Value,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Products.Add(newProduct);
                    created++;

                    movementLogs.Add(BuildStockMovement(
                        userId: userId,
                        product: newProduct,
                        previousQuantity: 0,
                        newQuantity: newProduct.StockQuantity,
                        movementType: "IMPORT",
                        reason: $"Import ile yeni ürün oluşturuldu (satır #{row.RowNumber})",
                        source: "products/import-stock",
                        actorName: actorName,
                        referenceId: importBatchId));

                    codeMap[codeKey] = new List<Product> { newProduct };
                }

                if (movementLogs.Count > 0)
                {
                    _context.StockMovements.AddRange(movementLogs);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Stok aktarımı tamamlandı. Güncellenen: {updated}, yeni: {created}, atlanan: {skipped}.",
                    summary = new
                    {
                        totalRows = rows.Count,
                        updated,
                        created,
                        skipped,
                        mode = allowUpsert ? "upsert" : "update_only"
                    },
                    skippedRows = skippedRows.Take(100)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Stok aktarım hatası: {ex.Message}");
            }
        }

        // 7. MANUEL STOK DÜZELTME (+/-)
        [HttpPost("{id}/adjust-stock")]
        public async Task<IActionResult> AdjustStock(Guid id, [FromBody] AdjustStockRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized("Kullanıcı doğrulanamadı.");

            if (request.DeltaQuantity == 0) return BadRequest("Değişim miktarı 0 olamaz.");

            var product = await _context.Products
                .Include(p => p.Catalog)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound("Ürün bulunamadı.");
            if (product.Catalog == null || product.Catalog.UserId != userId)
                return Unauthorized("Bu üründe stok düzenleme yetkiniz yok.");

            var previousQuantity = product.StockQuantity;
            var nextQuantity = previousQuantity + request.DeltaQuantity;
            if (nextQuantity < 0)
                return BadRequest("Stok eksiye düşemez.");

            product.StockQuantity = nextQuantity;
            product.UpdatedDate = DateTime.UtcNow;

            var movement = BuildStockMovement(
                userId: userId,
                product: product,
                previousQuantity: previousQuantity,
                newQuantity: nextQuantity,
                movementType: "ADJUSTMENT",
                reason: string.IsNullOrWhiteSpace(request.Reason) ? "Manuel stok düzeltmesi" : request.Reason.Trim(),
                source: "dashboard/parts",
                actorName: GetCurrentActorName(),
                referenceId: null);

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Stok başarıyla güncellendi.",
                productId = product.Id,
                code = product.Code,
                previousQuantity,
                newQuantity = nextQuantity,
                delta = request.DeltaQuantity
            });
        }

        // 8. STOK HAREKET GEÇMİŞİ
        [HttpGet("stock-movements")]
        public async Task<IActionResult> GetStockMovements([FromQuery] Guid? productId, [FromQuery] int limit = 50)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized("Kullanıcı doğrulanamadı.");

            limit = Math.Clamp(limit, 1, 200);

            var query = _context.StockMovements
                .AsNoTracking()
                .Where(m => m.UserId == userId);

            if (productId.HasValue && productId.Value != Guid.Empty)
            {
                query = query.Where(m => m.ProductId == productId.Value);
            }

            var rows = await query
                .OrderByDescending(m => m.CreatedDate)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.ProductId,
                    m.ProductCode,
                    m.ProductName,
                    m.PreviousQuantity,
                    m.DeltaQuantity,
                    m.NewQuantity,
                    m.MovementType,
                    m.Reason,
                    m.Source,
                    m.ActorName,
                    m.ReferenceId,
                    m.CreatedDate
                })
                .ToListAsync();

            return Ok(rows);
        }

        private static StockMovement BuildStockMovement(
            Guid userId,
            Product product,
            int previousQuantity,
            int newQuantity,
            string movementType,
            string reason,
            string source,
            string actorName,
            string? referenceId)
        {
            return new StockMovement
            {
                Id = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                UserId = userId,
                ProductId = product.Id,
                ProductCode = product.Code ?? string.Empty,
                ProductName = product.Name ?? string.Empty,
                PreviousQuantity = previousQuantity,
                NewQuantity = newQuantity,
                DeltaQuantity = newQuantity - previousQuantity,
                MovementType = movementType,
                Reason = string.IsNullOrWhiteSpace(reason) ? "-" : reason.Trim(),
                Source = source,
                ActorName = actorName,
                ReferenceId = referenceId
            };
        }

        private static string NormalizeCode(string? code)
        {
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
        }

        public sealed class AdjustStockRequest
        {
            public int DeltaQuantity { get; set; }
            public string? Reason { get; set; }
        }

        private sealed record StockImportSkipRow(int RowNumber, string? Code, string Reason);
    }
}
