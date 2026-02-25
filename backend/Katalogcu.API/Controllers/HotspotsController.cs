using FluentValidation;
using Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;
using Katalogcu.Application.Features.Hotspots.Commands.DeleteHotspot;
using Katalogcu.Application.Features.Hotspots.Commands.DetectHotspots;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class HotspotsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ILogger<HotspotsController> _logger;

        public HotspotsController(ISender sender, ILogger<HotspotsController> logger)
        {
            _sender = sender;
            _logger = logger;
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
                return StatusCode(500, new { error = "Hotspot tespiti sırasında hata oluştu", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateHotspotRequest request)
        {
            if (request.PageId == Guid.Empty)
            {
                return BadRequest("Geçersiz veri.");
            }

            try
            {
                var result = await _sender.Send(new CreateHotspotCommand(
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _sender.Send(new DeleteHotspotCommand(id));
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
    }
}
