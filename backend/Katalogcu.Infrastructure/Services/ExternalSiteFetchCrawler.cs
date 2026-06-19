using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalSiteFetchCrawler : IExternalSiteFetchCrawler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalSiteFetchCrawler> _logger;

    public ExternalSiteFetchCrawler(IHttpClientFactory httpClientFactory, ILogger<ExternalSiteFetchCrawler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PartalogBot/1.0");

            using var response = await client.GetAsync(baseUrl, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ExternalSiteFetchResult
                {
                    Succeeded = false,
                    ErrorSummary = $"Fetch başarısız: {(int)response.StatusCode}",
                    RawStatsJson = JsonSerializer.Serialize(new
                    {
                        statusCode = (int)response.StatusCode,
                        url = response.RequestMessage?.RequestUri?.ToString() ?? baseUrl
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
                    var absoluteUrl = new Uri(new Uri(baseUrl), x).ToString().TrimEnd('/');
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
                fetchedUrl = response.RequestMessage?.RequestUri?.ToString() ?? baseUrl,
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
                ErrorSummary = ex.Message,
                RawStatsJson = JsonSerializer.Serialize(new { error = ex.Message, url = baseUrl })
            };
        }
    }
}
