using Katalogcu.API.Services;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IPartalogAiService _aiService;
        private readonly AppDbContext _context;
        private readonly ILogger<ChatController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPublicLinkService _publicLinkService;
        private readonly IWebHostEnvironment _env;

        public ChatController(
            IPartalogAiService aiService,
            AppDbContext context,
            ILogger<ChatController> logger,
            IHttpClientFactory httpClientFactory,
            IPublicLinkService publicLinkService,
            IWebHostEnvironment env)
        {
            _aiService = aiService;
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _publicLinkService = publicLinkService;
            _env = env;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private static List<Guid> ParseCatalogIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
            try
            {
                var strIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();
                return strIds
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .Distinct()
                    .ToList();
            }
            catch
            {
                try
                {
                    var guidIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? new();
                    return guidIds.Where(g => g != Guid.Empty).Distinct().ToList();
                }
                catch
                {
                    return new List<Guid>();
                }
            }
        }

        private async Task<List<Guid>> ResolveAccessibleCatalogIds(Guid tokenUserId, PublicLinkPayload? publicPayload, List<Guid> requestedCatalogIds)
        {
            if (tokenUserId != Guid.Empty)
            {
                var userQuery = _context.Catalogs.AsNoTracking().Where(c => c.UserId == tokenUserId);
                if (requestedCatalogIds.Any())
                {
                    userQuery = userQuery.Where(c => requestedCatalogIds.Contains(c.Id));
                }
                return await userQuery.Select(c => c.Id).ToListAsync();
            }

            if (publicPayload != null)
            {
                var publicQuery = _context.Catalogs.AsNoTracking()
                    .Where(c => c.Status == "Published" && c.UserId == publicPayload.UserId);

                if (publicPayload.CatalogIds.Any())
                {
                    publicQuery = publicQuery.Where(c => publicPayload.CatalogIds.Contains(c.Id));
                }

                if (requestedCatalogIds.Any())
                {
                    publicQuery = publicQuery.Where(c => requestedCatalogIds.Contains(c.Id));
                }

                return await publicQuery.Select(c => c.Id).ToListAsync();
            }

            return new List<Guid>();
        }

        [HttpPost("ask")]
        [EnableRateLimiting("public-chat")]
        public async Task<IActionResult> Ask([FromForm] AiChatRequestWithHistoryDto request)
        {
            try
            {
                var tokenUserId = GetCurrentUserId();
                PublicLinkPayload? publicPayload = null;
                if (tokenUserId == Guid.Empty)
                {
                    if (!string.IsNullOrWhiteSpace(request.PublicToken))
                    {
                        publicPayload = _publicLinkService.Validate(request.PublicToken);
                    }
                }

                var catalogIdsJson = Request.HasFormContentType ? Request.Form["catalog_ids"].ToString() : null;
                var requestedCatalogIds = ParseCatalogIds(catalogIdsJson);
                var catalogIds = await ResolveAccessibleCatalogIds(tokenUserId, publicPayload, requestedCatalogIds);

                if (!catalogIds.Any())
                {
                    return BadRequest("Katalog bilgisi bulunamadı.");
                }

                _logger.LogInformation("Chat request catalogs: {CatalogCount}", catalogIds.Count);

                // 1. History Parse
                var chatHistory = new List<ChatMessageDto>();
                if (!string.IsNullOrEmpty(request.History))
                {
                    try
                    {
                        chatHistory = JsonConvert.DeserializeObject<List<ChatMessageDto>>(request.History) ?? new();
                    }
                    catch { _logger.LogWarning("History parse edilemedi, sohbet sıfırdan başlıyor."); }
                }

                var catalogIdStrings = catalogIds.Select(c => c.ToString()).ToList();

                // 2. Servis İsteği Hazırlığı
                var aiRequest = new AiChatRequestDto
                {
                    Text = request.Text,
                    Image = request.Image,
                    History = chatHistory,
                    CatalogIds = catalogIdStrings
                };

                // 3. AI Analizi (Python)
                var aiResponse = await _aiService.GetExpertChatResponseAsync(aiRequest);

                // --- NİYET ANALİZİ (Yeni yapı) ---
                string? searchTerm = null;
                string? intent = null;
                string? partCode = null;
                double? confidence = null;
                List<string> multiTerms = new();

                if (aiResponse.DebugIntent is JsonElement intentElement)
                {
                    if (intentElement.TryGetProperty("intent", out var it)) intent = it.GetString();
                    if (intentElement.TryGetProperty("part_name", out var pn)) searchTerm = pn.GetString();
                    if (intentElement.TryGetProperty("part_code", out var pc)) partCode = pc.GetString();

                    if (intentElement.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number)
                        confidence = cf.GetDouble();

                    // ✅ Multi-part yakalama (parts[])
                    multiTerms = ExtractPartsFromDebugIntent(intentElement);
                }

                if (confidence.HasValue && confidence.Value < 0.60)
                {
                    _logger.LogWarning("Low intent confidence: {Confidence} | Intent: {Intent} | Text: {Text}",
                        confidence.Value, intent ?? "n/a", request.Text);
                }

                // CHAT intent’te arama yapma
                if (string.Equals(intent, "CHAT", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = aiResponse.Answer ?? "Buyur ustam?",
                        Products = new List<EnrichedPartResult>(),
                        DebugInfo = $"Intent: {intent} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}"
                    });
                }

                // ✅ HELP intent
                if (string.Equals(intent, "HELP", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = "Ustam, hangi bilgiyi istersin? (fiyat, stok, uyumluluk, parça kodu) diye sor.",
                        Products = new List<EnrichedPartResult>(),
                        DebugInfo = $"Intent: HELP | Confidence: {confidence?.ToString("0.00") ?? "n/a"}"
                    });
                }

                // ✅ SEARCH intent + multi-part -> yan yana listele (SEMANTIC öncelikli)
                if (string.Equals(intent, "SEARCH", StringComparison.OrdinalIgnoreCase) && multiTerms.Count > 1)
                {
                    // ✅ Önce Python semantik kaynakları (varsa)
                    if (aiResponse.Sources != null && aiResponse.Sources.Any())
                    {
                        var compareGroups = new List<CompareGroupDto>();

                        var grouped = aiResponse.Sources
                            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                            .GroupBy(s => s.Query ?? string.Empty);

                        foreach (var group in grouped)
                        {
                            var products = await EnrichPythonSourcesAsync(group.ToList(), catalogIds);

                            compareGroups.Add(new CompareGroupDto
                            {
                                Query = string.IsNullOrWhiteSpace(group.Key) ? "Genel" : group.Key,
                                Results = products
                            });
                        }

                        var anyResults = compareGroups.Any(g => g.Results.Any());

                        return Ok(new ChatResponseDto
                        {
                            ReplySuggestion = anyResults
                                ? (aiResponse.Answer ?? "Birden fazla parça için sonuçları ayrı ayrı listeledim.")
                                : "Birden fazla parça istedin ama uygun sonuç bulamadım.",
                            Products = new List<EnrichedPartResult>(),
                            CompareGroups = compareGroups,
                            DebugInfo = $"Intent: SEARCH | Terms: {string.Join(", ", multiTerms)}"
                        });
                    }

                    // ✅ Fallback: eski kod araması
                    var compareGroupsFallback = new List<CompareGroupDto>();

                    foreach (var term in multiTerms)
                    {
                        var results = await SearchByCodeAsync(term, catalogIds);
                        var products = await EnrichResultsAsync(results, catalogIds);

                        compareGroupsFallback.Add(new CompareGroupDto
                        {
                            Query = term,
                            Results = products
                        });
                    }

                    var anyFallback = compareGroupsFallback.Any(g => g.Results.Any());

                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = anyFallback
                            ? (aiResponse.Answer ?? "Birden fazla parça için sonuçları ayrı ayrı listeledim.")
                            : "Birden fazla parça istedin ama uygun sonuç bulamadım.",
                        Products = new List<EnrichedPartResult>(),
                        CompareGroups = compareGroupsFallback,
                        DebugInfo = $"Intent: SEARCH | Terms: {string.Join(", ", multiTerms)}"
                    });
                }

                // ✅ Intent bazlı özel akışlar
                var intentQuery = partCode ?? searchTerm ?? request.Text;

                if (string.Equals(intent, "PRICE", StringComparison.OrdinalIgnoreCase))
                {
                    var priceResults = await SearchByCodeAsync(intentQuery, catalogIds);
                    var priceProducts = await EnrichResultsAsync(priceResults, catalogIds);

                    if (!priceProducts.Any())
                    {
                        return Ok(new ChatResponseDto
                        {
                            ReplySuggestion = "Fiyat için uygun parça bulamadım. Kod veya isim net mi?",
                            Products = new List<EnrichedPartResult>(),
                            DebugInfo = $"Intent: PRICE | Code: {intentQuery}"
                        });
                    }

                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = aiResponse.Answer ?? $"Fiyat bilgisi bulunan {priceProducts.Count} parça buldum.",
                        Products = priceProducts,
                        DebugInfo = $"Intent: PRICE | Code: {intentQuery}"
                    });
                }

                if (string.Equals(intent, "STOCK", StringComparison.OrdinalIgnoreCase))
                {
                    var stockResults = await SearchByCodeAsync(intentQuery, catalogIds);
                    var stockProducts = await EnrichResultsAsync(stockResults, catalogIds);

                    if (!stockProducts.Any())
                    {
                        return Ok(new ChatResponseDto
                        {
                            ReplySuggestion = "Stok için uygun parça bulamadım.",
                            Products = new List<EnrichedPartResult>(),
                            DebugInfo = $"Intent: STOCK | Code: {intentQuery}"
                        });
                    }

                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = aiResponse.Answer ?? "Stok durumlarını listeledim.",
                        Products = stockProducts,
                        DebugInfo = $"Intent: STOCK | Code: {intentQuery}"
                    });
                }

                if (string.Equals(intent, "COMPATIBILITY", StringComparison.OrdinalIgnoreCase))
                {
                    var compResults = await SearchByCodeAsync(intentQuery, catalogIds);
                    var compProducts = await EnrichResultsAsync(compResults, catalogIds);

                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = compProducts.Any()
                            ? (aiResponse.Answer ?? "Uyumlu model bilgilerini listeledim.")
                            : "Uyumluluk için parça bulunamadı.",
                        Products = compProducts,
                        DebugInfo = $"Intent: COMPATIBILITY | Code: {intentQuery}"
                    });
                }

                if (string.Equals(intent, "COMPARE", StringComparison.OrdinalIgnoreCase))
                {
                    var compareQuery = partCode ?? searchTerm ?? request.Text;
                    var terms = ExtractCompareTerms(compareQuery);

                    var compareGroups = new List<CompareGroupDto>();

                    foreach (var term in terms)
                    {
                        var compareResults = await SearchByCodeAsync(term, catalogIds);
                        var compareProducts = await EnrichResultsAsync(compareResults, catalogIds);

                        compareGroups.Add(new CompareGroupDto
                        {
                            Query = term,
                            Results = compareProducts
                        });
                    }

                    return Ok(new ChatResponseDto
                    {
                        ReplySuggestion = compareGroups.Any()
                            ? "Karşılaştırma için parçaları yan yana listeledim."
                            : "Karşılaştırma için uygun parça bulamadım.",
                        Products = new List<EnrichedPartResult>(),
                        CompareGroups = compareGroups,
                        DebugInfo = $"Intent: COMPARE | Terms: {string.Join(", ", terms)}"
                    });
                }

                // 4. PARÇA LİSTESİ HAZIRLIĞI
                List<EnrichedPartResult> finalProducts = new();

                // SENARYO A: Python kaynak bulduysa
                if (aiResponse.Sources != null && aiResponse.Sources.Any())
                {
                    finalProducts = await EnrichPythonSourcesAsync(aiResponse.Sources, catalogIds);
                }
                // SENARYO B: Python bulamadıysa ama Kod yakaladıysa
                else if (!string.IsNullOrWhiteSpace(partCode) && IsPartNumber(partCode))
                {
                    var fallbackResults = await SearchByCodeAsync(partCode, catalogIds);
                    finalProducts = await EnrichResultsAsync(fallbackResults, catalogIds);
                }
                else if (!string.IsNullOrWhiteSpace(searchTerm) && IsPartNumber(searchTerm))
                {
                    var fallbackResults = await SearchByCodeAsync(searchTerm, catalogIds);
                    finalProducts = await EnrichResultsAsync(fallbackResults, catalogIds);
                }

                // 5. ACİL MÜDAHALE (Kod araması)
                if (IsPartNumber(request.Text) && finalProducts.Count == 0)
                {
                    var directResults = await SearchByCodeAsync(request.Text, catalogIds);
                    if (directResults.Any())
                    {
                        finalProducts = await EnrichResultsAsync(directResults, catalogIds);
                        aiResponse.Answer = $"Aradığınız {request.Text} kodlu ürün için veritabanında {finalProducts.Count} sonuç buldum.";
                    }
                }

                // 7. CEVAP DÖN
                return Ok(new ChatResponseDto
                {
                    ReplySuggestion = aiResponse.Answer ?? "Üzgünüm, sonuç bulunamadı.",
                    Products = finalProducts,
                    DebugInfo = $"Intent: {intent ?? "Yok"} | Search: {searchTerm ?? "Yok"} | Code: {partCode ?? "Yok"} | Confidence: {confidence?.ToString("0.00") ?? "n/a"}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat Controller Hatası");
                return StatusCode(500, new { error = "Sistem hatası: " + ex.Message });
            }
        }

        private const int StreamBufferSize = 4096;

        [HttpPost("ask-stream")]
        [EnableRateLimiting("public-chat")]
        public async Task AskStream([FromForm] AiChatRequestWithHistoryDto request)
        {
            var tokenUserId = GetCurrentUserId();
            PublicLinkPayload? publicPayload = null;
            if (tokenUserId == Guid.Empty)
            {
                if (!string.IsNullOrWhiteSpace(request.PublicToken))
                {
                    publicPayload = _publicLinkService.Validate(request.PublicToken);
                }
            }

            var catalogIdsJson = Request.HasFormContentType ? Request.Form["catalog_ids"].ToString() : null;
            var requestedCatalogIds = ParseCatalogIds(catalogIdsJson);
            var catalogIds = await ResolveAccessibleCatalogIds(tokenUserId, publicPayload, requestedCatalogIds);

            if (!catalogIds.Any())
            {
                Response.StatusCode = 400;
                return;
            }

            var catalogIdStrings = catalogIds.Select(c => c.ToString()).ToList();

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            var httpClient = _httpClientFactory.CreateClient("PartalogAi");

            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent(request.Text ?? ""), "text");
            formContent.Add(new StringContent(request.History ?? "[]"), "history");
            formContent.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(catalogIdStrings)), "catalog_ids");
            if (request.Image != null)
            {
                var imageContent = new StreamContent(request.Image.OpenReadStream());
                formContent.Add(imageContent, "file", request.Image.FileName);
            }

            try
            {
                var requestMsg = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream") { Content = formContent };
                using var pythonResponse = await httpClient.SendAsync(requestMsg,
                    HttpCompletionOption.ResponseHeadersRead);

                using var stream = await pythonResponse.Content.ReadAsStreamAsync();
                var buffer = new byte[StreamBufferSize];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, HttpContext.RequestAborted)) > 0)
                {
                    await Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* Client disconnected */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AskStream proxy hatası");
            }
        }

        [HttpPost("visual-feedback")]
        [EnableRateLimiting("public-feedback")]
        public async Task<IActionResult> SaveVisualFeedback([FromForm] VisualFeedbackRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    if (!string.IsNullOrWhiteSpace(request.PublicToken))
                    {
                        var payload = _publicLinkService.Validate(request.PublicToken);
                        if (payload != null) userId = payload.UserId;
                    }
                }

                if (userId == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "Geçerli kullanıcı veya public token gerekli." });
                }

                if (request.Image == null)
                {
                    return BadRequest(new { success = false, message = "Fotoğraf zorunlu." });
                }

                if (string.IsNullOrWhiteSpace(request.PartName) && string.IsNullOrWhiteSpace(request.PartCode))
                {
                    return BadRequest(new { success = false, message = "partName veya partCode zorunlu." });
                }

                if (userId != Guid.Empty)
                {
                    request.UserId = userId.ToString();
                }

                var result = await _aiService.SaveVisualFeedbackAsync(request);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Visual feedback kaydetme hatası.");
                return StatusCode(500, new { success = false, message = "Sistem hatası oluştu." });
            }
        }

        [HttpPost("feedback")]
        [EnableRateLimiting("public-feedback")]
        public async Task<IActionResult> SaveChatFeedback([FromBody] ChatFeedbackRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { success = false, message = "Geçersiz geri bildirim verisi." });

                if (string.IsNullOrWhiteSpace(request.ReplySuggestion))
                    return BadRequest(new { success = false, message = "replySuggestion zorunludur." });

                var userId = GetCurrentUserId();
                var isPublic = false;

                if (userId == Guid.Empty && !string.IsNullOrWhiteSpace(request.PublicToken))
                {
                    var payload = _publicLinkService.Validate(request.PublicToken);
                    if (payload != null)
                    {
                        userId = payload.UserId;
                        isPublic = true;
                    }
                }

                if (userId == Guid.Empty)
                    return BadRequest(new { success = false, message = "Geçerli kullanıcı veya public token gerekli." });

                var sourceCodes = (request.SourceCodes ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant())
                    .Distinct()
                    .Take(30)
                    .ToList();

                var record = new ChatFeedbackRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTime.UtcNow,
                    UserId = userId,
                    IsPublic = isPublic,
                    Helpful = request.Helpful,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    UserQuery = string.IsNullOrWhiteSpace(request.UserQuery) ? null : request.UserQuery.Trim(),
                    ReplySuggestion = request.ReplySuggestion.Trim(),
                    SourceCodes = sourceCodes,
                    MessageId = string.IsNullOrWhiteSpace(request.MessageId) ? null : request.MessageId.Trim(),
                    ConversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? null : request.ConversationId.Trim(),
                    UserAgent = Request.Headers.UserAgent.ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                };

                var feedbackDir = Path.Combine(_env.ContentRootPath, "App_Data", "chat-feedback");
                Directory.CreateDirectory(feedbackDir);
                var feedbackPath = Path.Combine(feedbackDir, "index.jsonl");

                var line = System.Text.Json.JsonSerializer.Serialize(record) + Environment.NewLine;
                await System.IO.File.AppendAllTextAsync(feedbackPath, line, Encoding.UTF8);

                return Ok(new { success = true, id = record.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat feedback kaydetme hatası.");
                return StatusCode(500, new { success = false, message = "Sistem hatası oluştu." });
            }
        }

        // --- YARDIMCI METODLAR ---

        private static List<string> ExtractPartsFromDebugIntent(JsonElement intentElement)
        {
            var terms = new List<string>();

            if (intentElement.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("part_code", out var pc) && pc.ValueKind == JsonValueKind.String)
                    {
                        var value = pc.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            terms.Add(value);
                            continue;
                        }
                    }

                    if (part.TryGetProperty("part_name", out var pn) && pn.ValueKind == JsonValueKind.String)
                    {
                        var value = pn.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            terms.Add(value);
                        }
                    }
                }
            }

            return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> ExtractCompareTerms(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var separators = new[] { " ve ", " & ", ",", ";", "/" };
            var parts = separators.Aggregate(new List<string> { text }, (list, sep) =>
                list.SelectMany(x => x.Split(sep, StringSplitOptions.RemoveEmptyEntries)).ToList()
            );

            return parts
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<List<EnrichedPartResult>> EnrichPythonSourcesAsync(List<ChatSourceDto> sources, List<Guid> catalogIds)
        {
            var codes = sources.Where(s => !string.IsNullOrEmpty(s.Code)).Select(s => s.Code).Distinct().ToList();
            if (!codes.Any()) return new();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => codes.Contains(p.Code) && catalogIds.Contains(p.CatalogId))
                .ToListAsync();

            var productDict = products.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First());

            var catalogItems = await _context.CatalogItems
                .AsNoTracking()
                .Where(ci => codes.Contains(ci.PartCode) && catalogIds.Contains(ci.CatalogId))
                .ToListAsync();

            var itemDict = catalogItems
                .GroupBy(ci => ci.PartCode)
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.PartName) && x.PartName != "Unknown Part")
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Description))
                    .First());

            var enrichedList = new List<EnrichedPartResult>();

            foreach (var source in sources)
            {
                if (string.IsNullOrEmpty(source.Code)) continue;

                productDict.TryGetValue(source.Code, out var product);
                itemDict.TryGetValue(source.Code, out var catItem);

                // ✅ Legacy fallback
                var sourceModel = source.Model ?? source.LegacyModel;
                var sourceDesc = source.Description ?? source.LegacyDescription;

                string finalName = catItem?.PartName;
                if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = source.Name;
                if ((string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") && !string.IsNullOrWhiteSpace(catItem?.Description)) finalName = catItem.Description;
                if (string.IsNullOrWhiteSpace(finalName) || finalName == "Unknown Part") finalName = $"Parça {source.Code}";

                enrichedList.Add(new EnrichedPartResult
                {
                    Id = catItem?.Id ?? Guid.Empty,
                    Code = source.Code,
                    Name = finalName,
                    Description = catItem?.Description ?? sourceDesc,
                    Model = sourceModel,
                    CatalogId = catItem?.CatalogId ?? Guid.Empty,
                    PageNumber = catItem?.PageNumber,
                    StockStatus = product != null ? "Stokta Var" : "Stokta Yok",
                    Price = product?.Price,
                    ImageUrl = !string.IsNullOrWhiteSpace(catItem?.VisualImageUrl)
                        ? catItem.VisualImageUrl
                        : product?.ImageUrl
                });
            }
            return enrichedList;
        }

        private async Task<List<CatalogItem>> SearchByCodeAsync(string? term, List<Guid> catalogIds)
        {
            if (string.IsNullOrWhiteSpace(term) || !catalogIds.Any()) return new List<CatalogItem>();
            var code = term.Trim().ToUpperInvariant();

            return await _context.CatalogItems
                .AsNoTracking()
                .Where(ci =>
                    catalogIds.Contains(ci.CatalogId) &&
                    (ci.RefNumber == code || ci.PartCode == code || ci.PartCode.StartsWith(code)))
                .OrderBy(ci => ci.PartCode.Length)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<EnrichedPartResult>> EnrichResultsAsync(List<CatalogItem> items, List<Guid> catalogIds)
        {
            if (items.Count == 0 || !catalogIds.Any()) return new();

            var codes = items.Select(i => i.PartCode).Distinct().ToList();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => codes.Contains(p.Code) && catalogIds.Contains(p.CatalogId))
                .ToListAsync();

            var productDict = products.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First());

            var cleanCatalogItems = await _context.CatalogItems
                .AsNoTracking()
                .Where(ci => codes.Contains(ci.PartCode) && catalogIds.Contains(ci.CatalogId))
                .ToListAsync();

            var bestItemsDict = cleanCatalogItems
                .GroupBy(ci => ci.PartCode)
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.PartName) && x.PartName != "Unknown Part")
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Description))
                    .First());

            return items.Select(item =>
            {
                productDict.TryGetValue(item.PartCode ?? "", out var product);
                bestItemsDict.TryGetValue(item.PartCode ?? "", out var bestItem);
                var targetItem = bestItem ?? item;

                string displayName = targetItem.PartName;
                if (string.IsNullOrWhiteSpace(displayName) || displayName == "Unknown Part")
                {
                    displayName = !string.IsNullOrWhiteSpace(targetItem.Description) ? targetItem.Description : $"Parça {targetItem.PartCode}";
                }

                return new EnrichedPartResult
                {
                    Id = targetItem.Id,
                    Code = targetItem.PartCode ?? "",
                    Name = displayName,
                    Description = targetItem.Description,
                    CatalogId = targetItem.CatalogId,
                    PageNumber = targetItem.PageNumber,
                    StockStatus = product != null ? "Stokta Var" : "Stokta Yok",
                    Price = product?.Price,
                    ImageUrl = !string.IsNullOrWhiteSpace(targetItem.VisualImageUrl)
                        ? targetItem.VisualImageUrl
                        : product?.ImageUrl
                };
            }).ToList();
        }

        private bool IsPartNumber(string? term)
        {
            if (string.IsNullOrWhiteSpace(term)) return false;
            return term.Length > 2 && term.Any(char.IsDigit);
        }
    }

    #region DTOs
    public class AiChatRequestWithHistoryDto
    {
        public string? Text { get; set; }
        public IFormFile? Image { get; set; }
        public string? History { get; set; }
        public string? PublicToken { get; set; }
    }

    public record ChatResponseDto
    {
        public string ReplySuggestion { get; init; } = string.Empty;
        public List<EnrichedPartResult> Products { get; init; } = new();
        public string? DebugInfo { get; init; }

        // ✅ Yan yana karşılaştırma için
        public List<CompareGroupDto>? CompareGroups { get; init; }
    }

    public record CompareGroupDto
    {
        public string Query { get; init; } = string.Empty;
        public List<EnrichedPartResult> Results { get; init; } = new();
    }

    public record EnrichedPartResult
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Model { get; init; }
        public Guid CatalogId { get; init; }
        public string? PageNumber { get; init; }
        public string StockStatus { get; init; } = "Bilinmiyor";
        public decimal? Price { get; init; }
        public string? ImageUrl { get; init; }
    }

    public class ChatFeedbackRequestDto
    {
        public bool Helpful { get; set; }
        public string? Reason { get; set; }
        public string? UserQuery { get; set; }
        public string? ReplySuggestion { get; set; }
        public List<string>? SourceCodes { get; set; }
        public string? PublicToken { get; set; }
        public string? MessageId { get; set; }
        public string? ConversationId { get; set; }
    }

    public class ChatFeedbackRecord
    {
        public string Id { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid UserId { get; set; }
        public bool IsPublic { get; set; }
        public bool Helpful { get; set; }
        public string? Reason { get; set; }
        public string? UserQuery { get; set; }
        public string ReplySuggestion { get; set; } = string.Empty;
        public List<string> SourceCodes { get; set; } = new();
        public string? MessageId { get; set; }
        public string? ConversationId { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }
    #endregion
}
