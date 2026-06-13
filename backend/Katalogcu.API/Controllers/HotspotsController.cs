using FluentValidation;
using Katalogcu.API.Services;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;
using Katalogcu.Application.Features.Hotspots.Commands.DeleteHotspot;
using Katalogcu.Application.Features.Hotspots.Commands.DetectHotspots;
using Katalogcu.Application.Features.Hotspots.Commands.UpdateHotspot;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Claims;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class HotspotsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ILogger<HotspotsController> _logger;
        private readonly IHotspotRepository _hotspotRepository;
        private readonly IPartalogAiService _partalogAiService;
        private readonly IWebHostEnvironment _env;

        public HotspotsController(
            ISender sender,
            ILogger<HotspotsController> logger,
            IHotspotRepository hotspotRepository,
            IPartalogAiService partalogAiService,
            IWebHostEnvironment env)
        {
            _sender = sender;
            _logger = logger;
            _hotspotRepository = hotspotRepository;
            _partalogAiService = partalogAiService;
            _env = env;
        }

        [HttpPost("detect/{pageId}")]
        public async Task<IActionResult> DetectHotspots(Guid pageId)
        {
            try
            {
                _logger.LogInformation("🔍 Sayfa {PageId} için YOLO ile hotspot tespiti başlıyor...", pageId);
                var result = await _sender.Send(new DetectHotspotsCommand(pageId));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(new { error = result.ErrorMessage }),
                        "validation" => BadRequest(new { error = result.ErrorMessage }),
                        _ => StatusCode(500, new { error = result.ErrorMessage ?? "Hotspot tespit hatası" })
                    };
                }

                var response = result.Value!;
                _logger.LogInformation("✅ {Count} hotspot sonucu işlendi", response.DetectedCount);
                return Ok(new
                {
                    message = response.Message,
                    pageId = response.PageId,
                    detectedCount = response.DetectedCount,
                    hotspots = response.Hotspots
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotspot tespit hatası");
                return StatusCode(500, new { error = "Hotspot tespiti sırasında beklenmeyen bir hata oluştu." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateHotspotRequest request)
        {
            if (request.PageId == Guid.Empty)
            {
                return BadRequest("Geçersiz veri.");
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _sender.Send(new CreateHotspotCommand(
                    userId,
                    request.PageId,
                    request.Left,
                    request.Top,
                    request.Width,
                    request.Height,
                    request.Label,
                    request.IsAiDetected,
                    request.AiConfidence,
                    request.ProductId));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Hotspot oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHotspotRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _sender.Send(new UpdateHotspotCommand(
                    id,
                    userId,
                    request.Left,
                    request.Top,
                    request.Width,
                    request.Height,
                    request.Label,
                    request.ProductId));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Hotspot güncellenemedi.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _sender.Send(new DeleteHotspotCommand(id, userId));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(new { message = result.ErrorMessage ?? "Hotspot bulunamadı" }),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Hotspot silinemedi.")
                    };
                }

                return NoContent();
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/read-label")]
        public async Task<IActionResult> ReadLabel(Guid id, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var hotspot = await _hotspotRepository.GetHotspotByIdForUserAsync(id, userId, cancellationToken);
            if (hotspot?.Page is null)
            {
                return NotFound(new { error = "Hotspot bulunamadı." });
            }

            if (string.IsNullOrWhiteSpace(hotspot.Page.ImageUrl))
            {
                return BadRequest(new { error = "Sayfa görseli bulunamadı." });
            }

            var filePath = GetPhysicalPath(hotspot.Page.ImageUrl);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "Sayfa görseli sunucuda bulunamadı." });
            }

            try
            {
                await using var pageStream = System.IO.File.OpenRead(filePath);
                using var image = await Image.LoadAsync(pageStream, cancellationToken);

                var cropX = ClampToInt(image.Width * (hotspot.Left / 100.0), 0, image.Width - 1);
                var cropY = ClampToInt(image.Height * (hotspot.Top / 100.0), 0, image.Height - 1);
                var cropWidth = Math.Max(8, ClampToInt(image.Width * (hotspot.Width / 100.0), 1, image.Width));
                var cropHeight = Math.Max(8, ClampToInt(image.Height * (hotspot.Height / 100.0), 1, image.Height));
                var padding = Math.Max(6, (int)Math.Round(Math.Max(cropWidth, cropHeight) * 0.22));

                var rectX = Math.Max(0, cropX - padding);
                var rectY = Math.Max(0, cropY - padding);
                var rectWidth = Math.Min(image.Width - rectX, cropWidth + (padding * 2));
                var rectHeight = Math.Min(image.Height - rectY, cropHeight + (padding * 2));

                using var cropped = image.Clone(ctx => ctx.Crop(new Rectangle(rectX, rectY, rectWidth, rectHeight)));
                await using var memory = new MemoryStream();
                await cropped.SaveAsPngAsync(memory, cancellationToken);
                memory.Position = 0;

                var formFile = new FormFile(memory, 0, memory.Length, "file", $"{hotspot.Id}.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                };

                var result = await _partalogAiService.ReadHotspotLabelAsync(formFile.ToUploadedFile());
                return Ok(new ReadHotspotLabelResponse
                {
                    Success = result.Success,
                    Label = result.Label,
                    Confidence = result.Confidence,
                    Message = result.Message,
                    CropWidth = rectWidth,
                    CropHeight = rectHeight
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotspot OCR okunamadı. HotspotId={HotspotId}", id);
                return StatusCode(500, new { error = "Hotspot OCR sırasında beklenmeyen bir hata oluştu." });
            }
        }

        public sealed class CreateHotspotRequest
        {
            public Guid PageId { get; set; }
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string? Label { get; set; }
            public bool IsAiDetected { get; set; }
            public double AiConfidence { get; set; }
            public Guid? ProductId { get; set; }
        }

        public sealed class UpdateHotspotRequest
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string? Label { get; set; }
            public Guid? ProductId { get; set; }
        }

        public sealed class ReadHotspotLabelResponse
        {
            public bool Success { get; set; }
            public string? Label { get; set; }
            public double Confidence { get; set; }
            public string Message { get; set; } = string.Empty;
            public int CropWidth { get; set; }
            public int CropHeight { get; set; }
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

        private static int ClampToInt(double value, int min, int max)
        {
            return Math.Clamp((int)Math.Round(value), min, max);
        }

        private string GetPhysicalPath(string url)
        {
            var fileName = Path.GetFileName(url);

            var pathPages = Path.Combine(_env.WebRootPath, "uploads", "pages", fileName);
            if (System.IO.File.Exists(pathPages)) return pathPages;

            var pathRoot = Path.Combine(_env.WebRootPath, "uploads", fileName);
            if (System.IO.File.Exists(pathRoot)) return pathRoot;

            return pathPages;
        }
    }
}
