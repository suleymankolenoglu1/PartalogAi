using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text; // Encoding için gerekli
using Katalogcu.Application.Common.Exceptions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

// --- SERVİS (IMPLEMENTATION) ---
public class PartalogAiService : IPartalogAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PartalogAiService> _logger;
    private readonly AiServiceOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public PartalogAiService(HttpClient httpClient, ILogger<PartalogAiService> logger)
        : this(httpClient, logger, Options.Create(new AiServiceOptions()))
    {
    }

    [ActivatorUtilitiesConstructor]
    public PartalogAiService(HttpClient httpClient, ILogger<PartalogAiService> logger, IOptions<AiServiceOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        _httpClient.Timeout = _options.GetLongRunningTimeout();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    // --- 1. YOLO (HOTSPOT TESPİTİ) ---
    public async Task<List<Hotspot>> DetectHotspotsAsync(UploadedFile file, Guid pageId, bool throwOnFailure = false)
    {
        try
        {
            var responseJson = await SendFileStreamAsync(file, "/api/hotspot/detect");
            var result = JsonSerializer.Deserialize<YoloResponseDto>(responseJson, _jsonOptions);

            if (result == null || !result.Success || result.Hotspots == null)
            {
                if (throwOnFailure)
                {
                    throw new CatalogAiRetryableException(
                        "YOLOv8 hotspot detection",
                        "YOLO servisi geçersiz veya eksik cevap döndürdü.");
                }

                return new List<Hotspot>();
            }

            return result.Hotspots.Select(d => new Hotspot
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                Left = d.LeftPercent,
                Top = d.TopPercent,
                Width = d.WidthPercent,
                Height = d.HeightPercent,
                Label = d.Label,
                IsAiDetected = true,
                AiConfidence = d.Confidence,
                CreatedDate = DateTime.UtcNow
            }).ToList();
        }
        catch (Exception ex) when (!throwOnFailure)
        {
            _logger.LogError(ex, "YOLO servisi hatası.");
            return new List<Hotspot>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YOLO servisi hatası.");
            throw ex as CatalogAiRetryableException
                ?? new CatalogAiRetryableException(
                    "YOLOv8 hotspot detection",
                    "YOLO servisi çağrısı başarısız oldu.",
                    ex);
        }
    }

    public async Task<HotspotLabelReadResultDto> ReadHotspotLabelAsync(UploadedFile file)
    {
        try
        {
            var responseJson = await SendFileStreamAsync(file, "/api/hotspot/read-label");
            var result = JsonSerializer.Deserialize<HotspotLabelReadResultDto>(responseJson, _jsonOptions);
            return result ?? new HotspotLabelReadResultDto
            {
                Success = false,
                Message = "OCR cevabı okunamadı."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotspot OCR servisi hatası.");
            return new HotspotLabelReadResultDto
            {
                Success = false,
                Message = "Hotspot OCR servisine ulaşılamadı."
            };
        }
    }

    // --- 2. GEMINI (TABLO OKUMA) ---
    public async Task<List<ProductItemDto>> ExtractTableAsync(byte[] fileBytes, int pageNumber, bool throwOnFailure = false)
    {
        try
        {
            _logger.LogInformation(
                "📋 Table extraction request başladı | Page={PageNumber} | Bytes={ByteLength}",
                pageNumber,
                fileBytes.Length);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", "page.jpg");

            var response = await _httpClient.PostAsync($"/api/table/extract?page_number={pageNumber}", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "⚠️ Table extraction başarısız döndü | Page={PageNumber} | Status={StatusCode}",
                    pageNumber,
                    (int)response.StatusCode);

                if (throwOnFailure)
                {
                    throw new CatalogAiRetryableException(
                        "OCR table extraction",
                        $"Tablo okuma servisi başarısız döndü: {(int)response.StatusCode}");
                }

                return new List<ProductItemDto>();
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TableResponseDto>(responseJson, _jsonOptions);

            if (result == null || !result.Success || result.Tables == null)
            {
                _logger.LogWarning(
                    "⚠️ Table extraction geçersiz cevap döndürdü | Page={PageNumber} | BodyLength={BodyLength}",
                    pageNumber,
                    responseJson.Length);

                if (throwOnFailure)
                {
                    throw new CatalogAiRetryableException(
                        "OCR table extraction",
                        "Tablo okuma servisi geçersiz veya eksik cevap döndürdü.");
                }

                return new List<ProductItemDto>();
            }

            var products = result.Tables.SelectMany(t => t.Products ?? new List<ProductItemDto>()).ToList();
            _logger.LogInformation(
                "✅ Table extraction tamamlandı | Page={PageNumber} | Tables={TableCount} | Products={ProductCount}",
                pageNumber,
                result.Tables.Count,
                products.Count);

            return products;
        }
        catch (Exception ex) when (!throwOnFailure)
        {
            _logger.LogError(ex, "Tablo okuma servisi hatası.");
            return new List<ProductItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tablo okuma servisi hatası.");
            throw ex as CatalogAiRetryableException
                ?? new CatalogAiRetryableException(
                    "OCR table extraction",
                    "Tablo okuma servisi çağrısı başarısız oldu.",
                    ex);
        }
    }

    // --- 3. SAYFA ANALİZİ ---
    public async Task<PageAnalysisResult> AnalyzePageAsync(byte[] fileBytes)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", "page.jpg");

            var response = await _httpClient.PostAsync("/api/analysis/analyze-page-title", content);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PageAnalysisResult>(responseJson, _jsonOptions);
                return result ?? new PageAnalysisResult();
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "⚠️ Sayfa analiz servisi başarısız döndü | Status={StatusCode} | Body={Body}",
                (int)response.StatusCode,
                errorBody);

            throw new CatalogAiRetryableException(
                "AI page analysis",
                $"Sayfa analiz servisi başarısız döndü: {(int)response.StatusCode}");
        }
        catch (CatalogAiRetryableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sayfa analiz servisi hatası.");
            throw new CatalogAiRetryableException(
                "AI page analysis",
                "Sayfa analiz servisi çağrısı başarısız oldu.",
                ex);
        }
    }

    // --- 4. EXPERT AI CHAT (GÜNCELLENMİŞ VERSİYON) ---
    public async Task<AiChatResponseDto> GetExpertChatResponseAsync(AiChatRequestDto request)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(request.Text ?? ""), "text");
            // History null ise boş liste gönder
            var historyJson = JsonSerializer.Serialize(request.History ?? new List<ChatMessageDto>(), _jsonOptions);
            content.Add(new StringContent(historyJson), "history");

            if (request.CatalogIds != null && request.CatalogIds.Any())
            {
                var idsJson = JsonSerializer.Serialize(request.CatalogIds, _jsonOptions);
                content.Add(new StringContent(idsJson), "catalog_ids");
            }

            if (!string.IsNullOrWhiteSpace(request.ContextJson))
            {
                content.Add(new StringContent(request.ContextJson), "context_json");
            }

            if (!string.IsNullOrWhiteSpace(request.UserPlan))
            {
                content.Add(new StringContent(request.UserPlan), "user_plan");
            }
            if (request.AiLimitPerMonth.HasValue)
            {
                content.Add(new StringContent(request.AiLimitPerMonth.Value.ToString()), "ai_limit_per_month");
            }
            if (request.AiUsedThisMonth.HasValue)
            {
                content.Add(new StringContent(request.AiUsedThisMonth.Value.ToString()), "ai_used_this_month");
            }
            if (!string.IsNullOrWhiteSpace(request.PolicyThresholdOverride))
            {
                content.Add(new StringContent(request.PolicyThresholdOverride), "policy_threshold_override");
            }

            if (request.Image != null)
            {
                var fileStream = request.Image.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(request.Image.ContentType)
                        ? ResolveImageContentType(request.Image.FileName)
                        : request.Image.ContentType);
                content.Add(fileContent, "file", request.Image.FileName);
            }

            using var timeoutCts = new CancellationTokenSource(_options.GetChatTimeout());
            var response = await _httpClient.PostAsync("/api/chat/expert-chat", content, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var (message, reason) = TryReadAiError(errorMsg);
                    var fallbackReason = string.IsNullOrWhiteSpace(reason) ? "ai_capacity_limited" : reason;
                    _logger.LogWarning(
                        "Chat AI isteği kapasite/rate limit nedeniyle reddedildi | Reason={Reason} | Body={Body}",
                        fallbackReason,
                        errorMsg);

                    return BuildFallbackChatResponse(
                        string.IsNullOrWhiteSpace(message)
                            ? "AI kapasitesi şu an dolu. Lütfen birkaç saniye sonra tekrar deneyin."
                            : message,
                        fallbackReason);
                }

                _logger.LogError("Chat API Hatası ({StatusCode}): {Body}", response.StatusCode, errorMsg);
                return BuildFallbackChatResponse(
                    "AI servisine şu an ulaşılamıyor. Lütfen daha sonra tekrar deneyin.",
                    "ai_upstream_error");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AiChatResponseDto>(jsonResponse, _jsonOptions);

            // 🔥 BOŞ DÖNERSE VARSAYILAN MESAJ
            return result ?? new AiChatResponseDto { Answer = "Cevap anlaşılamadı." };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Chat servisi zaman aşımına uğradı.");
            return BuildFallbackChatResponse(
                "AI yanıtı zaman aşımına uğradı. Lütfen tekrar deneyin.",
                "ai_timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat servisi hatası.");
            return BuildFallbackChatResponse("Sistem hatası oluştu.", "ai_exception");
        }
    }

    private static AiChatResponseDto BuildFallbackChatResponse(string answer, string reason)
    {
        return new AiChatResponseDto
        {
            Answer = answer,
            Sources = [],
            DebugIntent = new Dictionary<string, object?>
            {
                ["fallback"] = true,
                ["fallback_reason"] = reason
            }
        };
    }

    private static (string? Message, string? Reason) TryReadAiError(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.String)
                {
                    return (detail.GetString(), null);
                }

                if (detail.ValueKind == JsonValueKind.Object)
                {
                    return (
                        TryGetStringProperty(detail, "message"),
                        TryGetStringProperty(detail, "reason"));
                }
            }

            return (
                TryGetStringProperty(root, "message"),
                TryGetStringProperty(root, "reason"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    // --- 4.1 Görsel Geri Bildirim Kaydı ---
    public async Task<VisualFeedbackResponseDto> SaveVisualFeedbackAsync(VisualFeedbackRequestDto request)
    {
        try
        {
            if (request.Image == null)
            {
                return new VisualFeedbackResponseDto
                {
                    Success = false,
                    Message = "Görsel dosyası zorunlu."
                };
            }

            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(request.PartName ?? string.Empty), "part_name");
            content.Add(new StringContent(request.PartCode ?? string.Empty), "part_code");
            content.Add(new StringContent(request.MachineBrand ?? string.Empty), "machine_brand");
            content.Add(new StringContent(request.MachineType ?? string.Empty), "machine_type");
            content.Add(new StringContent(request.UserId ?? string.Empty), "user_id");
            content.Add(new StringContent(request.Note ?? string.Empty), "note");

            var fileStream = request.Image.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.Image.ContentType ?? "image/jpeg");
            content.Add(fileContent, "file", request.Image.FileName);

            var response = await _httpClient.PostAsync("/api/chat/visual-feedback", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Visual feedback API Hatası ({StatusCode}): {Body}", response.StatusCode, responseBody);
                return new VisualFeedbackResponseDto
                {
                    Success = false,
                    Message = "Visual feedback servisi hatası."
                };
            }

            var result = JsonSerializer.Deserialize<VisualFeedbackResponseDto>(responseBody, _jsonOptions);
            return result ?? new VisualFeedbackResponseDto
            {
                Success = false,
                Message = "Visual feedback cevabı anlaşılamadı."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visual feedback kaydetme hatası.");
            return new VisualFeedbackResponseDto
            {
                Success = false,
                Message = "Sistem hatası oluştu."
            };
        }
    }

    // --- 5. EĞİTİM TETİKLEYİCİ ---
    public async Task TriggerTrainingAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/admin/train", null);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("✅ AI Sözlük Eğitimi başarıyla tetiklendi.");
            else
                _logger.LogWarning($"⚠️ AI Eğitimi tetiklenemedi. Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ AI Trigger hatası.");
        }
    }

    // --- 6. CANONICAL INGESTION SEARCH TEXT ---
    public async Task<IReadOnlyList<string>> BuildSearchTextsAsync(
        IReadOnlyList<IngestionSearchTextRequest> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(rows, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                "/api/v1/ingestion/build-search-texts",
                jsonContent,
                cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "SearchText build API Hatası ({StatusCode}): {Body}",
                    response.StatusCode,
                    responseJson);
                throw new CatalogAiRetryableException(
                    "canonical search text build",
                    $"SearchText üretim servisi başarısız döndü: {(int)response.StatusCode}");
            }

            var searchTexts = JsonSerializer.Deserialize<List<string>>(responseJson, _jsonOptions);
            if (searchTexts == null || searchTexts.Count != rows.Count)
            {
                throw new CatalogAiRetryableException(
                    "canonical search text build",
                    $"SearchText üretim servisi geçersiz sayıda sonuç döndürdü. Expected={rows.Count}, Actual={searchTexts?.Count ?? 0}");
            }

            return searchTexts;
        }
        catch (CatalogAiRetryableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchText üretim servisi hatası.");
            throw new CatalogAiRetryableException(
                "canonical search text build",
                "SearchText üretim servisi çağrısı başarısız oldu.",
                ex);
        }
    }

    // --- 7. EMBEDDING (VEKTÖR) ALMA ---
    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var payload = new { text = text };
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/api/embed", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Embedding API Hatası ({response.StatusCode}): {err}");
                return null;
            }

            var resJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EmbeddingResponseDto>(resJson, _jsonOptions);

            return result?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding servisi hatası.");
            return null;
        }
    }

    // --- YARDIMCI METODLAR ---
    private async Task<string> SendFileStreamAsync(UploadedFile file, string relativeUrl)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? ResolveImageContentType(file.FileName)
            : file.ContentType;
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", file.FileName);

        var response = await _httpClient.PostAsync(relativeUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API Hatası: {response.StatusCode}");
        }
        return await response.Content.ReadAsStringAsync();
    }

    private static string ResolveImageContentType(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            _ => "image/png"
        };
    }

    // --- DAHİLİ DTO SINIFLARI (Internal) ---
    private class YoloResponseDto
    {
        public bool Success { get; set; }
        public List<YoloHotspotDto>? Hotspots { get; set; }
    }
    private class YoloHotspotDto
    {
        public string? Label { get; set; }
        public double Confidence { get; set; }
        [JsonPropertyName("left_percent")] public double LeftPercent { get; set; }
        [JsonPropertyName("top_percent")] public double TopPercent { get; set; }
        [JsonPropertyName("width_percent")] public double WidthPercent { get; set; }
        [JsonPropertyName("height_percent")] public double HeightPercent { get; set; }
    }
    private class TableResponseDto
    {
        public bool Success { get; set; }
        public List<TableResultDto>? Tables { get; set; }
    }
    private class TableResultDto { public List<ProductItemDto>? Products { get; set; } }

    private class EmbeddingResponseDto
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

}
