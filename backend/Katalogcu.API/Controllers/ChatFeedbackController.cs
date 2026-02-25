using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatFeedbackController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatFeedbackController> _logger;

        public ChatFeedbackController(IWebHostEnvironment env, ILogger<ChatFeedbackController> logger)
        {
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetFeedbacks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? helpful = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var indexPath = Path.Combine(_env.ContentRootPath, "App_Data", "chat-feedback", "index.jsonl");
            if (!System.IO.File.Exists(indexPath))
                return Ok(new { items = new List<object>(), total = 0, page, pageSize });

            try
            {
                var lines = System.IO.File.ReadAllLines(indexPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                if (helpful.HasValue)
                {
                    lines = lines.Where(line =>
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("Helpful", out var helpfulEl)
                                && (helpfulEl.ValueKind == JsonValueKind.True || helpfulEl.ValueKind == JsonValueKind.False))
                            {
                                return helpfulEl.GetBoolean() == helpful.Value;
                            }
                        }
                        catch { }
                        return false;
                    }).ToList();
                }

                var total = lines.Count;
                var items = lines
                    .AsEnumerable()
                    .Reverse()
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
                _logger.LogError(ex, "Chat feedback kayıtları okunamadı.");
                return StatusCode(500, "Kayıtlar okunamadı.");
            }
        }
    }
}
