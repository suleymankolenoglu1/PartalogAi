using Microsoft.AspNetCore.Authorization;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Katalogcu.API.Services;
using Katalogcu.Application.Features.Products.Commands.AdjustStock;
using Katalogcu.Application.Features.Products.Commands.CreateProduct;
using Katalogcu.Application.Features.Products.Commands.DeleteProduct;
using Katalogcu.Application.Features.Products.Commands.ImportProducts;
using Katalogcu.Application.Features.Products.Commands.ImportStock;
using Katalogcu.Application.Features.Products.Queries.GetCatalogProducts;
using Katalogcu.Application.Features.Products.Queries.GetOwnedProducts;
using Katalogcu.Application.Features.Products.Queries.GetStockMovements;
using FluentValidation;
using MediatR;
using System.Security.Claims; // ✨ User ID okumak için

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")] // 🔒 Yönetim paneli kullanıcıları
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ExcelService _excelService;
        private readonly IPublicAccessTokenService _publicAccessTokenService;
        private readonly ISender _sender;

        public ProductsController(ExcelService excelService, IPublicAccessTokenService publicAccessTokenService, ISender sender)
        {
            _excelService = excelService;
            _publicAccessTokenService = publicAccessTokenService;
            _sender = sender;
        }

        // 🛠️ Yardımcı Metod: Token'dan UserID'yi (Guid) okur
        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private (Guid userId, bool isPublic, PublicAccessPayloadDto? publicPayload) ResolveAccess(string? token)
        {
            var tokenUserId = GetCurrentUserId();
            if (tokenUserId != Guid.Empty) return (tokenUserId, false, null);

            if (!string.IsNullOrWhiteSpace(token))
            {
                var payload = _publicAccessTokenService.Validate(token);
                if (payload != null) return (payload.UserId, true, payload);
            }

            return (Guid.Empty, true, null);
        }

        // 1. TÜM ÜRÜNLERİ GETİR (SADECE BENİM OLANLAR)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _sender.Send(new GetOwnedProductsQuery());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Ürünler alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 2. KATALOĞA GÖRE GETİR (Vitrin için açık bırakıldı)
        [AllowAnonymous]
        [HttpGet("catalog/{catalogId}")]
        public async Task<IActionResult> GetByCatalog(Guid catalogId, [FromQuery] string? token)
        {
            var access = ResolveAccess(token);
            if (access.userId == Guid.Empty) return BadRequest("Kullanıcı bilgisi bulunamadı.");

            try
            {
                var result = await _sender.Send(new GetCatalogProductsQuery(
                    access.userId,
                    catalogId,
                    access.isPublic,
                    access.publicPayload?.CatalogIds));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Katalog ürünleri alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 3. YENİ ÜRÜN EKLE
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            try
            {
                var result = await _sender.Send(new CreateProductCommand(
                    request.CatalogId,
                    request.Name ?? string.Empty,
                    request.Code ?? string.Empty,
                    request.OemNo,
                    request.Price,
                    request.StockQuantity,
                    request.ImageUrl,
                    request.Category,
                    request.Description,
                    request.PageNumber,
                    request.RefNo));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => BadRequest(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Ürün oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        // 4. ÜRÜN SİL (GÜÇLENDİRİLMİŞ)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _sender.Send(new DeleteProductCommand(id));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Silme hatası.")
                    };
                }

                return NoContent();
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
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
            var validationError = UploadValidation.ValidateSpreadsheet(file, required: true, allowCsv: false);
            if (!string.IsNullOrWhiteSpace(validationError))
                return BadRequest(validationError);

            try 
            {
                var parsed = _excelService.ParseProducts(file, Guid.Empty);
                var command = new ImportProductsCommand(
                    catalogId,
                    parsed.Select(x => new ImportProductRowInput
                    {
                        Name = x.Name,
                        Code = x.Code,
                        Category = x.Category,
                        Price = x.Price,
                        StockQuantity = x.StockQuantity,
                        Description = x.Description
                    }).ToList());

                var result = await _sender.Send(command);
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => BadRequest(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Yükleme hatası.")
                    };
                }

                var response = result.Value!;
                return Ok(new { message = $"{response.Count} adet ürün başarıyla yüklendi!", count = response.Count });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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
            var validationError = UploadValidation.ValidateSpreadsheet(file, required: true, allowCsv: true);
            if (!string.IsNullOrWhiteSpace(validationError))
                return BadRequest(validationError);

            try
            {
                var rows = _excelService.ParseStockRows(file);
                var command = new ImportStockCommand(
                    catalogId,
                    mode,
                    rows.Select(x => new ImportStockRowInput
                    {
                        RowNumber = x.RowNumber,
                        Code = x.Code,
                        StockQuantity = x.StockQuantity,
                        Price = x.Price,
                        Name = x.Name,
                        Category = x.Category,
                        Description = x.Description
                    }).ToList());

                var result = await _sender.Send(command);
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => BadRequest(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Stok aktarım hatası.")
                    };
                }

                var response = result.Value!;
                var skippedRows = response.SkippedRows.Take(100).ToList();

                return Ok(new
                {
                    message = $"Stok aktarımı tamamlandı. Güncellenen: {response.Updated}, yeni: {response.Created}, atlanan: {response.Skipped}.",
                    summary = new
                    {
                        totalRows = response.TotalRows,
                        updated = response.Updated,
                        created = response.Created,
                        skipped = response.Skipped,
                        mode = response.Mode,
                        processed = response.Updated + response.Created
                    },
                    skippedRows,
                    skippedRowsReturned = skippedRows.Count,
                    skippedRowsTruncated = response.SkippedRows.Count > skippedRows.Count
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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
            try
            {
                var result = await _sender.Send(new AdjustStockCommand(id, request.DeltaQuantity, request.Reason));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Stok güncelleme hatası.")
                    };
                }

                var response = result.Value!;
                return Ok(new
                {
                    message = "Stok başarıyla güncellendi.",
                    productId = response.ProductId,
                    code = response.Code,
                    previousQuantity = response.PreviousQuantity,
                    newQuantity = response.NewQuantity,
                    delta = response.Delta
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 8. STOK HAREKET GEÇMİŞİ
        [HttpGet("stock-movements")]
        public async Task<IActionResult> GetStockMovements([FromQuery] Guid? productId, [FromQuery] int limit = 50)
        {
            try
            {
                var result = await _sender.Send(new GetStockMovementsQuery(productId, limit));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Stok hareketleri alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public sealed class AdjustStockRequest
        {
            public int DeltaQuantity { get; set; }
            public string? Reason { get; set; }
        }

        public sealed class CreateProductRequest
        {
            public Guid CatalogId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string? OemNo { get; set; }
            public decimal Price { get; set; }
            public int StockQuantity { get; set; }
            public string? ImageUrl { get; set; }
            public string? Category { get; set; }
            public string? Description { get; set; }
            public string? PageNumber { get; set; }
            public int RefNo { get; set; }
        }
    }
}
