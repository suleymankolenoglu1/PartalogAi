using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class VisualFeedbackController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VisualFeedbackController> _logger;

        public VisualFeedbackController(IWebHostEnvironment env, ILogger<VisualFeedbackController> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Python AI servisinin kaydettiği visual-feedback kayıtlarını listeler.
        /// index.jsonl dosyasını okur (her satır bir JSON kaydı).
        /// </summary>
        [HttpGet]
        public IActionResult GetFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // Python servisi bu dosyaya yazar
            var indexPath = Path.Combine(_env.ContentRootPath, "..", "partalog-ai", "static", "user-generated-parts", "index.jsonl");

            if (!System.IO.File.Exists(indexPath))
                return Ok(new { items = new List<object>(), total = 0, page, pageSize });

            try
            {
                var lines = System.IO.File.ReadAllLines(indexPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                var total = lines.Count;
                var items = lines
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(line =>
                    {
                        try { return JsonSerializer.Deserialize<JsonElement>(line); }
                        catch { return (JsonElement?)null; }
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();

                return Ok(new { items, total, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VisualFeedback index.jsonl okunamadı");
                return StatusCode(500, "Kayıtlar okunamadı.");
            }
        }
    }
}
