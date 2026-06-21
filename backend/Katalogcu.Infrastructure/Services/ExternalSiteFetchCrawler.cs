using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalSiteFetchCrawler : IExternalSiteFetchCrawler
{
    private readonly ISafeExternalHttpClient _httpClient;
    private readonly ILogger<ExternalSiteFetchCrawler> _logger;

    public ExternalSiteFetchCrawler(ISafeExternalHttpClient httpClient, ILogger<ExternalSiteFetchCrawler> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(
                HttpMethod.Get,
                baseUrl,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var fetchedUri = response.RequestMessage?.RequestUri ?? new Uri(baseUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new ExternalSiteFetchResult
                {
                    Succeeded = false,
                    ErrorSummary = $"Fetch başarısız: {(int)response.StatusCode}",
                    RawStatsJson = JsonSerializer.Serialize(new
                    {
                        statusCode = (int)response.StatusCode,
                        url = fetchedUri.ToString()
                    })
                };
            }

            var titleMatch = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : null;

            var hrefMatches = Regex.Matches(html, "href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
            var hrefs = hrefMatches
                .Select(x => x.Groups[1].Value.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("#"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var candidateProducts = hrefs
                .Where(x =>
                    x.Contains("/urun", StringComparison.OrdinalIgnoreCase) ||
                    x.Contains("/product", StringComparison.OrdinalIgnoreCase) ||
                    x.Contains("/products", StringComparison.OrdinalIgnoreCase) ||
                    x.Contains("part", StringComparison.OrdinalIgnoreCase))
                .Select(x =>
                {
                    var absoluteUrl = new Uri(fetchedUri, x).ToString().TrimEnd('/');
                    return new CrawledProduct
                    {
                        SourceUrl = absoluteUrl,
                        CanonicalUrl = absoluteUrl,
                        RawPayloadJson = JsonSerializer.Serialize(new { href = x })
                    };
                })
                .DistinctBy(x => x.SourceUrl, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var stats = new
            {
                title,
                fetchedUrl = fetchedUri.ToString(),
                statusCode = (int)response.StatusCode,
                hrefCount = hrefs.Count,
                candidateProductLinks = candidateProducts.Count,
                contentLength = html.Length
            };

            _logger.LogInformation("External site fetch crawl tamamlandı: {BaseUrl} | Href={HrefCount} | CandidateProducts={CandidateProducts}",
                baseUrl,
                hrefs.Count,
                candidateProducts.Count);

            return new ExternalSiteFetchResult
            {
                Succeeded = true,
                ProductCount = candidateProducts.Count,
                SkuCoverage = 0,
                OemCoverage = 0,
                RawStatsJson = JsonSerializer.Serialize(stats),
                Products = candidateProducts
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External site fetch crawl hata verdi: {BaseUrl}", baseUrl);
            return new ExternalSiteFetchResult
            {
                Succeeded = false,
                ErrorSummary = "Dış site güvenli biçimde doğrulanamadı veya erişilemedi.",
                RawStatsJson = JsonSerializer.Serialize(new { error = "external_fetch_failed", url = baseUrl })
            };
        }
    }
}
