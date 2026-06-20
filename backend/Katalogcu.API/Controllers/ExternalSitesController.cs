using FluentValidation;
using Katalogcu.API.Services;
using Katalogcu.Application.Features.ExternalSites.Commands.CreateExternalSite;
using Katalogcu.Application.Features.ExternalSites.Commands.DeleteExternalSite;
using Katalogcu.Application.Features.ExternalSites.Commands.ImportExternalSiteProductsFromFile;
using Katalogcu.Application.Features.ExternalSites.Commands.StartExternalSiteCrawl;
using Katalogcu.Application.Features.ExternalSites.Commands.UpdateExternalSite;
using Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSiteById;
using Katalogcu.Application.Features.ExternalSites.Queries.GetExternalProductsBySite;
using Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSites;
using Katalogcu.Application.Features.ExternalSites.Queries.GetManualImportHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using NPOI.XSSF.UserModel;
using System.Security.Claims;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PrivilegedUser")]
[Route("api/external-sites")]
[ApiController]
public sealed class ExternalSitesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<ExternalSitesController> _logger;

    public ExternalSitesController(ISender sender, ILogger<ExternalSitesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetExternalSites()
    {
        try
        {
            var result = await _sender.Send(new GetExternalSitesQuery());
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Site listesi alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpGet("import-template")]
    public IActionResult DownloadImportTemplate()
    {
        var workbook = new XSSFWorkbook();

        var sampleSheet = workbook.CreateSheet("Dolu ornek");
        var descriptionSheet = workbook.CreateSheet("Aciklama");

        var sampleHeader = sampleSheet.CreateRow(0);
        var sampleColumns = new[] { "title", "partCode", "sku", "url", "brand", "category", "oemNumber" };
        for (var i = 0; i < sampleColumns.Length; i++)
        {
            sampleHeader.CreateCell(i).SetCellValue(sampleColumns[i]);
        }

        sampleSheet.CreateRow(1).SetValues("Motor Kafasi", "MF7900-HEAD", "70003404", "https://site.com/urun/motor-kafasi", "Mitsufuji", "Motor > Kapak", "238100001");
        sampleSheet.CreateRow(2).SetValues("Kapak Destegi", "MF7900-BRACKET", "13403407", "https://site.com/urun/kapak-destegi", "Mitsufuji", "Motor > Destek", "3530011");
        sampleSheet.CreateRow(3).SetValues("Somun M4", "NM6040001SC", "NM6040001SC", "", "Mitsufuji", "Baglanti", "NM6040001SC");

        var descriptionHeader = descriptionSheet.CreateRow(0);
        descriptionHeader.CreateCell(0).SetCellValue("Kolon");
        descriptionHeader.CreateCell(1).SetCellValue("Aciklama");
        descriptionHeader.CreateCell(2).SetCellValue("Zorunlu mu");

        var descriptions = new[]
        {
            new[] { "title", "Urun veya parca basligi", "title veya sku/partCode zorunlu" },
            new[] { "partCode", "Katalogtaki parca kodu", "Hayir" },
            new[] { "sku", "Sitedeki stok veya urun kodu", "title veya sku/partCode zorunlu" },
            new[] { "url", "Urun detay linki", "Hayir" },
            new[] { "brand", "Marka bilgisi", "Hayir" },
            new[] { "category", "Kategori veya kategori yolu", "Hayir" },
            new[] { "oemNumber", "OEM numaralari, birden fazla ise virgulle ayrilabilir", "Hayir" }
        };

        for (var i = 0; i < descriptions.Length; i++)
        {
            descriptionSheet.CreateRow(i + 1).SetValues(descriptions[i]);
        }

        for (var i = 0; i < sampleColumns.Length; i++)
        {
            sampleSheet.AutoSizeColumn(i);
            descriptionSheet.AutoSizeColumn(i == 0 ? 0 : i > 2 ? 2 : i);
        }

        using var stream = new MemoryStream();
        workbook.Write(stream);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ornek_urun_listesi.xlsx");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExternalSiteById(Guid id)
    {
        try
        {
            var result = await _sender.Send(new GetExternalSiteByIdQuery(id));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Site detayı alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/products")]
    public async Task<IActionResult> GetExternalProducts(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sender.Send(new GetExternalProductsBySiteQuery(id, page, pageSize));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Taranan ürünler alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/imports")]
    public async Task<IActionResult> GetManualImportHistory(Guid id)
    {
        try
        {
            var result = await _sender.Send(new GetManualImportHistoryQuery(id));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Import geçmişi alınamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateExternalSite([FromBody] UpsertExternalSiteDto request)
    {
        try
        {
            var result = await _sender.Send(new CreateExternalSiteCommand(request.Name, request.BaseUrl, request.PreferredCrawlMode));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "duplicate" => BadRequest(result.ErrorMessage),
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Site kaydı oluşturulamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateExternalSite(Guid id, [FromBody] UpsertExternalSiteDto request)
    {
        try
        {
            var result = await _sender.Send(new UpdateExternalSiteCommand(id, request.Name, request.BaseUrl, request.PreferredCrawlMode, request.Status));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "duplicate" => BadRequest(result.ErrorMessage),
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Site kaydı güncellenemedi.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteExternalSite(Guid id)
    {
        try
        {
            var result = await _sender.Send(new DeleteExternalSiteCommand(id));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Site kaydı silinemedi.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:guid}/crawl")]
    [EnableRateLimiting("external-site-crawl")]
    public async Task<IActionResult> StartCrawl(Guid id)
    {
        try
        {
            var result = await _sender.Send(new StartExternalSiteCrawlCommand(id));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "not_found" => NotFound(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Tarama başlatılamadı.")
                };
            }

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:guid}/import")]
    [EnableRateLimiting("external-site-import")]
    public async Task<IActionResult> ImportProducts(Guid id, [FromForm] ExternalSiteImportRequest request)
    {
        var validationError = UploadValidation.ValidateExternalSiteImportFile(request.File);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequest(validationError);
        }

        try
        {
            _logger.LogInformation(
                "External site import started. SiteId={SiteId} UserId={UserId} FileName={FileName} FileType={FileType} ContentType={ContentType} FileSize={FileSize}",
                id,
                GetCurrentUserId(),
                request.File.FileName,
                request.FileType ?? Path.GetExtension(request.File.FileName).TrimStart('.'),
                request.File.ContentType,
                request.File.Length);

            var result = await _sender.Send(new ImportExternalSiteProductsFromFileCommand(
                id,
                request.File.ToUploadedFile(),
                request.FileType));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(result.ErrorMessage),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Dosya import edilemedi.")
                };
            }

            _logger.LogInformation(
                "External site import completed. SiteId={SiteId} UserId={UserId} ManualImportFileId={ManualImportFileId} RowCount={RowCount} ImportedProductCount={ImportedProductCount} FailedRowCount={FailedRowCount} Status={Status}",
                id,
                GetCurrentUserId(),
                result.Value!.ManualImportFileId,
                result.Value.RowCount,
                result.Value.ImportedProductCount,
                result.Value.FailedRowCount,
                result.Value.Status);

            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private string GetCurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
}

public sealed class UpsertExternalSiteDto
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string PreferredCrawlMode { get; set; } = "auto";
    public string Status { get; set; } = "active";
}

public sealed class ExternalSiteImportRequest
{
    public IFormFile File { get; set; } = null!;
    public string? FileType { get; set; }
}

file static class XssfRowExtensions
{
    public static void SetValues(this NPOI.SS.UserModel.IRow row, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            row.CreateCell(i).SetCellValue(values[i]);
        }
    }
}
