using Katalogcu.API.Services;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Katalogcu.API.Controllers
{
    [Authorize] // 🔒 Varsayılan: Giriş yapmış kullanıcılar
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;
        private readonly CatalogProcessorService _processorService;
        private readonly ILogger<CatalogsController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;

        public CatalogsController(
            AppDbContext context,
            PdfService pdfService,
            CatalogProcessorService processorService,
            ILogger<CatalogsController> logger,
            IWebHostEnvironment env,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _pdfService = pdfService;
            _processorService = processorService;
            _logger = logger;
            _env = env;
            _scopeFactory = scopeFactory;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid))
            {
                return guid;
            }
            return Guid.Empty;
        }

        private Guid ResolveUserId(Guid? userId)
        {
            var tokenUserId = GetCurrentUserId();
            if (tokenUserId != Guid.Empty) return tokenUserId;
            if (userId.HasValue && userId.Value != Guid.Empty) return userId.Value;
            return Guid.Empty;
        }

        // ==========================================
        // 🌍 PUBLIC VIEW (HERKESE AÇIK LİSTE)
        // ==========================================
        [AllowAnonymous] 
        [HttpGet("public")] 
        public async Task<IActionResult> GetPublicCatalogs()
        {
            var catalogs = await _context.Catalogs
                .AsNoTracking()
                .Where(c => c.Status == "Published")
                .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return Ok(catalogs);
        }

        // ==========================================
        // 🌍 PUBLIC VIEW (KULLANICIYA ÖZEL)
        // ==========================================
        [AllowAnonymous]
        [HttpGet("public/{userId:guid}")]
        public async Task<IActionResult> GetPublicCatalogsByUser(Guid userId)
        {
            if (userId == Guid.Empty) return BadRequest("Geçersiz kullanıcı.");

            var catalogs = await _context.Catalogs
                .AsNoTracking()
                .Where(c => c.Status == "Published" && c.UserId == userId)
                .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return Ok(catalogs);
        }

        // ==========================================
        // 📂 1. KATALOG TAŞIMA (KLASÖR YÖNETİMİ)
        // ==========================================
        [HttpPut("{id}/move")]
        public async Task<IActionResult> MoveCatalog(Guid id, [FromBody] MoveCatalogDto request)
        {
            var userId = GetCurrentUserId();

            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (catalog == null) return NotFound("Katalog bulunamadı.");

            if (request.FolderId.HasValue)
            {
                var folderExists = await _context.Folders
                    .AnyAsync(f => f.Id == request.FolderId.Value && f.UserId == userId);

                if (!folderExists) return BadRequest("Hedef klasör bulunamadı veya size ait değil.");
            }

            catalog.FolderId = request.FolderId;
            catalog.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Katalog başarıyla taşındı.", folderId = catalog.FolderId });
        }

        // ==========================================
        // 🤖 2. AI İŞLEMİ (PYTHON TETİKLEYİCİLİ)
        // ==========================================
        [HttpPost("{id}/start-ai-process")]
        public async Task<IActionResult> StartAutonomousProcess(Guid id)
        {
            var userId = GetCurrentUserId();
            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (catalog == null) return NotFound("Katalog bulunamadı veya yetkiniz yok.");

            if (catalog.Status == "Processing")
                return BadRequest("Bu katalog zaten işleniyor.");

            catalog.Status = "Processing";
            await _context.SaveChangesAsync();

            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    try
                    {
                        var scopedProcessor = scope.ServiceProvider.GetRequiredService<CatalogProcessorService>();
                        var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var scopedAiService = scope.ServiceProvider.GetRequiredService<IPartalogAiService>();

                        await scopedProcessor.ProcessCatalogAsync(id);

                        var cat = await scopedContext.Catalogs.FindAsync(id);
                        if (cat != null)
                        {
                            cat.Status = "Published";
                            cat.UpdatedDate = DateTime.UtcNow;
                            await scopedContext.SaveChangesAsync();
                        }

                        await scopedAiService.TriggerTrainingAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Arka plan işlem hatası: {id}");
                        using (var errorScope = _scopeFactory.CreateScope())
                        {
                            var errorDb = errorScope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var cat = await errorDb.Catalogs.FindAsync(id);
                            if (cat != null)
                            {
                                cat.Status = "Error";
                                await errorDb.SaveChangesAsync();
                            }
                        }
                    }
                }
            });

            return Accepted(new
            {
                message = "AI Analizi başlatıldı. İşlem bitince katalog otomatik olarak yayına alınacak.",
                catalogId = id,
                status = "Processing"
            });
        }

        // ==========================================
        // 📄 3. SAYFA ÖĞELERİNİ GETİR (RefNumber Uyumlu)
        // ==========================================
        [AllowAnonymous]
        [HttpGet("{id}/pages/{pageNumber}/items")]
        public async Task<IActionResult> GetPageItems(Guid id, string pageNumber, [FromQuery] Guid? userId)
        {
            var resolvedUserId = ResolveUserId(userId);
            if (resolvedUserId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            if (!int.TryParse(pageNumber, out int currentPage)) return BadRequest("Sayfa numarası geçersiz.");

            var catalogItems = await FetchItemsForPage(id, currentPage.ToString(), resolvedUserId);

            if (!catalogItems.Any()) catalogItems = await FetchItemsForPage(id, (currentPage + 1).ToString(), resolvedUserId);
            if (!catalogItems.Any() && currentPage > 1) catalogItems = await FetchItemsForPage(id, (currentPage - 1).ToString(), resolvedUserId);

            if (!catalogItems.Any()) return Ok(new List<object>());

            var itemCodes = catalogItems.Select(ci => ci.PartCode).Distinct().ToList();
            var stockedProducts = await _context.Products
                .Include(p => p.Catalog)
                .AsNoTracking()
                .Where(p => itemCodes.Contains(p.Code) && p.Catalog.UserId == resolvedUserId)
                .GroupBy(p => p.Code).Select(g => g.First()).ToDictionaryAsync(p => p.Code);

            var result = catalogItems.Select(item =>
            {
                var isStocked = stockedProducts.ContainsKey(item.PartCode);
                var product = isStocked ? stockedProducts[item.PartCode] : null;

                return new
                {
                    catalogItemId = item.Id,
                    refNo = item.RefNumber,
                    partCode = item.PartCode,
                    partName = item.PartName,
                    description = item.Description,
                    isStocked = isStocked,
                    productId = product?.Id,
                    price = product?.Price,
                    localName = product?.Name
                };
            });

            return Ok(result);
        }

        private async Task<List<CatalogItem>> FetchItemsForPage(Guid catalogId, string pageNum, Guid userId)
        {
            return await _context.CatalogItems
                .Include(ci => ci.Catalog)
                .AsNoTracking()
                .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNum && ci.Catalog.UserId == userId)
                .OrderBy(ci => ci.RefNumber)
                .ToListAsync();
        }

        // ==========================================
        // STANDART CRUD İŞLEMLERİ
        // ==========================================

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = GetCurrentUserId();
            var totalCatalogs = await _context.Catalogs.CountAsync(c => c.UserId == userId);
            var totalParts = await _context.Products.Include(p => p.Catalog).CountAsync(p => p.Catalog.UserId == userId);

            var pendingCount = await _context.Catalogs.Where(c => c.UserId == userId)
                .CountAsync(c => c.Status == "Processing" || c.Status == "Pending" || c.Status == "Uploading");

            var recentCatalogs = await _context.Catalogs.Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedDate).Take(5)
                .Select(c => new { c.Id, c.Name, c.Status, PartCount = _context.Products.Count(p => p.CatalogId == c.Id), c.CreatedDate })
                .ToListAsync();

            var visualEmbeddingCount = await _context.CatalogItems
                .CountAsync(ci => ci.Catalog.UserId == userId && ci.VisualEmbedding != null);

            return Ok(new
            {
                TotalCatalogs = totalCatalogs,
                TotalParts = totalParts,
                TotalViews = 0, // TODO: gerçek view tracking henüz implemente edilmedi
                PendingCount = pendingCount,
                RecentCatalogs = recentCatalogs,
                VisualEmbeddingCount = visualEmbeddingCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetCurrentUserId();
            var catalogs = await _context.Catalogs.Where(c => c.UserId == userId)
                .Include(c => c.Pages).OrderByDescending(c => c.CreatedDate).ToListAsync();
            return Ok(catalogs);
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? userId)
        {
            var resolvedUserId = ResolveUserId(userId);
            if (resolvedUserId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            var catalog = await _context.Catalogs
                .Include(c => c.Pages.OrderBy(p => p.PageNumber))
                .ThenInclude(p => p.Hotspots)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == resolvedUserId);

            if (catalog == null) return NotFound("Katalog bulunamadı.");
            return Ok(catalog);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Catalog catalog)
        {
            var userId = GetCurrentUserId();
            catalog.UserId = userId;
            catalog.CreatedDate = DateTime.UtcNow;
            catalog.Status = "Uploading";

            _context.Catalogs.Add(catalog);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(catalog.PdfUrl))
            {
                try
                {
                    var fileName = Path.GetFileName(catalog.PdfUrl);
                    var pageUrls = await _pdfService.ConvertPdfToImages(fileName);
                    int pageNum = 1;
                    var newPages = new List<CatalogPage>();
                    foreach (var imgPath in pageUrls)
                    {
                        var fullUrl = $"{Request.Scheme}://{Request.Host}/{imgPath}";
                        newPages.Add(new CatalogPage { CatalogId = catalog.Id, PageNumber = pageNum++, ImageUrl = fullUrl });
                    }
                    _context.CatalogPages.AddRange(newPages);
                    catalog.Status = "ReadyToProcess";
                    _context.Catalogs.Update(catalog);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PDF işleme hatası");
                    catalog.Status = "Error";
                    await _context.SaveChangesAsync();
                    return StatusCode(500, "PDF işlenirken hata oluştu.");
                }
            }
            return CreatedAtAction(nameof(GetById), new { id = catalog.Id }, catalog);
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            var userId = GetCurrentUserId();
            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (catalog == null) return NotFound();
            
            catalog.Status = "Published";
            catalog.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Katalog yayına alındı", status = catalog.Status });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();
            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (catalog == null) return NotFound("Katalog bulunamadı veya yetkiniz yok.");

            try
            {
                var productIds = await _context.Products.Where(p => p.CatalogId == id).Select(p => p.Id).ToListAsync();
                if (productIds.Any())
                {
                    await _context.OrderItems.Where(oi => productIds.Contains(oi.ProductId)).ExecuteDeleteAsync();
                    await _context.Hotspots.Where(h => productIds.Contains(h.ProductId.Value)).ExecuteDeleteAsync();
                }
                await _context.Products.Where(p => p.CatalogId == id).ExecuteDeleteAsync();
                await _context.CatalogItems.Where(ci => ci.CatalogId == id).ExecuteDeleteAsync();
                await _context.CatalogPages.Where(cp => cp.CatalogId == id).ExecuteDeleteAsync();
                _context.Catalogs.Remove(catalog);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silme işlemi hatası");
                return StatusCode(500, "Silme işlemi sırasında hata oluştu: " + ex.Message);
            }
        }

        [HttpDelete("{id}/pages/{pageId}/clear")]
        public async Task<IActionResult> ClearPageData(Guid id, Guid pageId)
        {
            var userId = GetCurrentUserId();
            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (catalog == null) return NotFound("Katalog bulunamadı veya yetkiniz yok.");
            var page = await _context.CatalogPages.FindAsync(pageId);
            if (page == null) return NotFound("Sayfa bulunamadı.");
            await _context.Hotspots.Where(h => h.PageId == pageId).ExecuteDeleteAsync();
            await _context.CatalogItems.Where(ci => ci.CatalogId == id && ci.PageNumber == page.PageNumber.ToString()).ExecuteDeleteAsync();
            return Ok(new { message = "Sayfa verileri temizlendi" });
        }
    }

    // --- DTO ---
    public class MoveCatalogDto
    {
        public Guid? FolderId { get; set; }
    }
}