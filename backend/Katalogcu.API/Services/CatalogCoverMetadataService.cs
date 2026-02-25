using System.Net.Http.Headers;
using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;

namespace Katalogcu.API.Services;

public sealed class CatalogCoverMetadataService : ICatalogCoverMetadataService
{
    private const string PythonApiUrl = "http://localhost:8000";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CatalogCoverMetadataService> _logger;

    public CatalogCoverMetadataService(
        IHttpClientFactory httpClientFactory,
        ILogger<CatalogCoverMetadataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<CatalogCoverMetadataDto?> AnalyzeAsync(byte[] fileBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", "cover.jpg");

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            using var response = await client.PostAsync(
                $"{PythonApiUrl}/api/table/extract-metadata",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<CatalogCoverMetadataDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kapak metadata analizi sırasında hata oluştu.");
            return null;
        }
    }
}
