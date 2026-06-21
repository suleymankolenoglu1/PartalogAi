using System.Text.Json;
using System.Text.RegularExpressions;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Katalogcu.Application.Features.ExternalSites.Commands;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalSitePlaywrightCrawler : IExternalSitePlaywrightCrawler
{
    private readonly ILogger<ExternalSitePlaywrightCrawler> _logger;

    public ExternalSitePlaywrightCrawler(ILogger<ExternalSitePlaywrightCrawler> logger)
    {
        _logger = logger;
    }

    public async Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "PartalogBot/1.0 (+playwright)"
            });

            await context.RouteAsync("**/*", async route =>
            {
                if (await ExternalSiteUrlSecurityValidator.IsSafeExternalUrlAsync(route.Request.Url, cancellationToken))
                {
                    await route.ContinueAsync();
                    return;
                }

                await route.AbortAsync();
            });

            var page = await context.NewPageAsync();
            await page.GotoAsync(baseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = 10000
                });
            }
            catch
            {
                // Network idle her sitede oluşmayabilir; DOM yüklendiyse devam ediyoruz.
            }

            var pageTitle = await page.TitleAsync();
            var linkData = await page.EvaluateAsync<List<PlaywrightLinkPayload>>(
                """
                () => Array.from(document.querySelectorAll('a[href]')).map(anchor => ({
                  href: anchor.href || anchor.getAttribute('href') || '',
                  text: (anchor.textContent || '').trim(),
                  title: (anchor.getAttribute('title') || '').trim()
                }))
                """);

            var productCandidates = (linkData ?? [])
                .Where(link => !string.IsNullOrWhiteSpace(link.Href))
                .Select(link => BuildProductCandidate(baseUrl, link))
                .Where(candidate => candidate is not null)
                .Cast<CrawledProduct>()
                .DistinctBy(candidate => candidate.SourceUrl, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var skuCoverage = CalculateCoverage(productCandidates, product => product.Sku ?? product.PartCode);
            var oemCoverage = CalculateCoverage(productCandidates, product => product.OemNumbers.FirstOrDefault());

            var stats = new
            {
                fetchedBy = "playwright",
                title = pageTitle,
                finalUrl = page.Url,
                anchorCount = linkData?.Count ?? 0,
                candidateProductLinks = productCandidates.Count,
                skuCoverage,
                oemCoverage
            };

            _logger.LogInformation(
                "External site Playwright crawl tamamlandı: {BaseUrl} | Anchor={AnchorCount} | CandidateProducts={CandidateProducts}",
                baseUrl,
                linkData?.Count ?? 0,
                productCandidates.Count);

            return new ExternalSiteFetchResult
            {
                Succeeded = true,
                ProductCount = productCandidates.Count,
                SkuCoverage = skuCoverage,
                OemCoverage = oemCoverage,
                RawStatsJson = JsonSerializer.Serialize(stats),
                Products = productCandidates
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External site Playwright crawl hata verdi: {BaseUrl}", baseUrl);
            return new ExternalSiteFetchResult
            {
                Succeeded = false,
                ErrorSummary = ex.Message,
                RawStatsJson = JsonSerializer.Serialize(new
                {
                    fetchedBy = "playwright",
                    error = ex.Message,
                    url = baseUrl
                })
            };
        }
    }

    private static CrawledProduct? BuildProductCandidate(string baseUrl, PlaywrightLinkPayload link)
    {
        var href = link.Href.Trim();
        if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || !LooksLikeProductUrl(href))
        {
            return null;
        }

        var absoluteUrl = BuildAbsoluteUrl(baseUrl, href);
        if (absoluteUrl is null)
        {
            return null;
        }

        var rawTitle = string.IsNullOrWhiteSpace(link.Text) ? link.Title : link.Text;
        var normalizedTitle = NormalizeWhitespace(rawTitle);
        var codeCandidates = ExtractCodes($"{absoluteUrl} {normalizedTitle}");
        var partCode = codeCandidates.FirstOrDefault();

        return new CrawledProduct
        {
            SourceUrl = absoluteUrl,
            CanonicalUrl = absoluteUrl,
            Title = string.IsNullOrWhiteSpace(normalizedTitle) ? null : normalizedTitle,
            Sku = partCode,
            PartCode = partCode,
            OemNumbers = codeCandidates,
            RawPayloadJson = JsonSerializer.Serialize(new
            {
                href = link.Href,
                text = link.Text,
                title = link.Title
            })
        };
    }

    private static bool LooksLikeProductUrl(string href)
    {
        return href.Contains("/urun", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/product", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/products", StringComparison.OrdinalIgnoreCase)
            || href.Contains("part", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildAbsoluteUrl(string baseUrl, string href)
    {
        try
        {
            return new Uri(new Uri(baseUrl), href).ToString().TrimEnd('/');
        }
        catch
        {
            return null;
        }
    }

    private static decimal CalculateCoverage(IReadOnlyCollection<CrawledProduct> products, Func<CrawledProduct, string?> selector)
    {
        if (products.Count == 0)
        {
            return 0;
        }

        var coveredCount = products.Count(product => !string.IsNullOrWhiteSpace(selector(product)));
        return Math.Round((decimal)coveredCount / products.Count * 100m, 2);
    }

    private static IReadOnlyList<string> ExtractCodes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return Regex.Matches(value, @"\b[A-Z0-9][A-Z0-9\-_]{4,}\b", RegexOptions.IgnoreCase)
            .Select(match => match.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private sealed class PlaywrightLinkPayload
    {
        public string Href { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }
}
