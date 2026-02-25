using Katalogcu.API.Contracts.Chat;
using Katalogcu.API.Services;
using FluentValidation;
using Katalogcu.Application.Features.Ai.Commands.AnalyzePageFromFile;
using Katalogcu.Application.Features.Ai.Commands.DetectHotspotsFromFile;
using Katalogcu.Application.Features.Ai.Commands.ExtractTableFromFile;
using Katalogcu.Application.Features.Ai.Commands.RunExpertChat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly ISender _sender;

        public AiController(ISender sender)
        {
            _sender = sender;
        }

        // 1. Hotspot Tespiti (YOLO) 
        [HttpPost("detect-hotspots")]
        public async Task<IActionResult> DetectHotspots(IFormFile file, [FromQuery] Guid pageId)
        {
            var validationError = UploadValidation.ValidateImage(file, required: true);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(validationError);
            }

            try
            {
                var result = await _sender.Send(new DetectHotspotsFromFileCommand(file, pageId));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Hotspot tespit edilemedi.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 2. Tablo Okuma (Gemini) 
        [HttpPost("extract-table")]
        public async Task<IActionResult> ExtractTable(IFormFile file, [FromQuery] int pageNumber)
        {
            var validationError = UploadValidation.ValidateImage(file, required: true);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(validationError);
            }

            try
            {
                var result = await _sender.Send(new ExtractTableFromFileCommand(file, pageNumber));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Tablo çıkarılamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 3. Sayfa Analizi (Başlık ve Tür)
        [HttpPost("analyze-page")]
        public async Task<IActionResult> AnalyzePage(IFormFile file)
        {
            var validationError = UploadValidation.ValidateImage(file, required: true);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(validationError);
            }

            try
            {
                var result = await _sender.Send(new AnalyzePageFromFileCommand(file));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Sayfa analiz edilemedi.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("expert-chat")]
        public async Task<IActionResult> ExpertChat([FromForm] AiChatRequestWithHistoryDto request)
        {
            var validationError = UploadValidation.ValidateImage(request.Image, required: false);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(validationError);
            }

            try
            {
                var result = await _sender.Send(new RunExpertChatCommand(
                    request.Text,
                    request.Image,
                    request.History,
                    null));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Expert chat çalıştırılamadı.")
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
}
