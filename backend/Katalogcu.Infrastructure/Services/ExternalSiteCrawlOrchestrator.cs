using Katalogcu.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalSiteCrawlOrchestrator : IExternalSiteCrawlOrchestrator
{
    private const int MinFetchProductCountForBrowserFallback = 20;
    private const decimal MinFetchSkuCoverageForBrowserFallback = 30m;

    private readonly IExternalSiteRepository _externalSiteRepository;
    private readonly IExternalSiteFetchCrawler _fetchCrawler;
    private readonly IExternalSitePlaywrightCrawler _playwrightCrawler;
    private readonly IExternalProductNormalizer _externalProductNormalizer;
    private readonly IExternalProductUpsertService _externalProductUpsertService;
    private readonly ILogger<ExternalSiteCrawlOrchestrator> _logger;

    public ExternalSiteCrawlOrchestrator(
        IExternalSiteRepository externalSiteRepository,
        IExternalSiteFetchCrawler fetchCrawler,
        IExternalSitePlaywrightCrawler playwrightCrawler,
        IExternalProductNormalizer externalProductNormalizer,
        IExternalProductUpsertService externalProductUpsertService,
        ILogger<ExternalSiteCrawlOrchestrator> logger)
    {
        _externalSiteRepository = externalSiteRepository;
        _fetchCrawler = fetchCrawler;
        _playwrightCrawler = playwrightCrawler;
        _externalProductNormalizer = externalProductNormalizer;
        _externalProductUpsertService = externalProductUpsertService;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid crawlId, CancellationToken cancellationToken)
    {
        var crawl = await _externalSiteRepository.GetCrawlByIdWithSiteAsync(crawlId, cancellationToken);
        if (crawl?.ExternalSite is null)
        {
            throw new InvalidOperationException($"External site crawl bulunamadı: {crawlId}");
        }

        crawl.Status = "running";
        crawl.StartedAtUtc = DateTime.UtcNow;
        crawl.ExternalSite.LastCrawlAtUtc = crawl.StartedAtUtc;
        crawl.ExternalSite.UpdatedDate = DateTime.UtcNow;
        await _externalSiteRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("External site crawl başladı: {CrawlId} | Site={SiteId}", crawl.Id, crawl.ExternalSiteId);

        var fetchResult = await _fetchCrawler.CrawlAsync(crawl.ExternalSite.BaseUrl, cancellationToken);
        crawl.ExecutionMode = "fetch";
        crawl.SkuCoverage = fetchResult.SkuCoverage;
        crawl.OemCoverage = fetchResult.OemCoverage;
        crawl.RawStatsJson = fetchResult.RawStatsJson;
        crawl.CompletedAtUtc = DateTime.UtcNow;
        crawl.UpdatedDate = DateTime.UtcNow;

        if (fetchResult.Succeeded)
        {
            var fetchNormalizedProducts = _externalProductNormalizer.Normalize(crawl.ExternalSiteId, crawl.Id, fetchResult.Products);
            crawl.ProductCount = fetchNormalizedProducts.Count;

            var needsPlaywrightFallback = ShouldEscalateToPlaywright(crawl.ProductCount, crawl.SkuCoverage);
            if (needsPlaywrightFallback)
            {
                await ExecutePlaywrightFallbackAsync(crawl, fetchResult, fetchNormalizedProducts, cancellationToken);
            }
            else
            {
                await _externalProductUpsertService.UpsertAsync(crawl.ExternalSiteId, crawl.Id, fetchNormalizedProducts, cancellationToken);
                if (fetchNormalizedProducts.Count > 0)
                {
                    await _externalProductUpsertService.MarkMissingInactiveAsync(
                        crawl.ExternalSiteId,
                        fetchNormalizedProducts.Select(x => x.Product.SourceUrl).ToArray(),
                        cancellationToken);
                }

                crawl.Status = "completed";
                crawl.ErrorSummary = null;
                crawl.RawStatsJson = MergeCrawlerStageStats(fetchResult.RawStatsJson, null, "none", crawl.ProductCount, crawl.SkuCoverage);
                crawl.ExternalSite.LastSuccessfulCrawlAtUtc = crawl.CompletedAtUtc;
            }
        }
        else
        {
            crawl.ProductCount = fetchResult.ProductCount;
            crawl.Status = "failed";
            crawl.ErrorSummary = fetchResult.ErrorSummary;
        }

        crawl.ExternalSite.UpdatedDate = DateTime.UtcNow;
        await _externalSiteRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("External site crawl tamamlandı: {CrawlId} | Status={Status}", crawl.Id, crawl.Status);
    }

    private static bool ShouldEscalateToPlaywright(int productCount, decimal? skuCoverage)
    {
        if (productCount < MinFetchProductCountForBrowserFallback)
        {
            return true;
        }

        var normalizedSkuCoverage = skuCoverage ?? 0;
        return normalizedSkuCoverage < MinFetchSkuCoverageForBrowserFallback;
    }

    private async Task ExecutePlaywrightFallbackAsync(
        Katalogcu.Domain.Entities.ExternalSiteCrawl crawl,
        Application.Common.Models.ExternalSiteFetchResult fetchResult,
        IReadOnlyList<Application.Common.Models.NormalizedExternalProductRecord> fetchNormalizedProducts,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetch sonucu yetersiz. Playwright fallback başlatılıyor: {CrawlId}", crawl.Id);

        var baseUrl = crawl.ExternalSite?.BaseUrl
            ?? throw new InvalidOperationException($"External site bilgisi bulunamadı: {crawl.Id}");

        var playwrightResult = await _playwrightCrawler.CrawlAsync(baseUrl, cancellationToken);
        var playwrightNormalizedProducts = playwrightResult.Succeeded
            ? _externalProductNormalizer.Normalize(crawl.ExternalSiteId, crawl.Id, playwrightResult.Products)
            : [];

        var playwrightProductCount = playwrightNormalizedProducts.Count;
        var playwrightSkuCoverage = playwrightResult.SkuCoverage;
        var needsManagedBrowserFallback = !playwrightResult.Succeeded || ShouldEscalateToPlaywright(playwrightProductCount, playwrightSkuCoverage);

        if (playwrightResult.Succeeded && playwrightNormalizedProducts.Count > 0)
        {
            crawl.ExecutionMode = "playwright";
            crawl.ProductCount = playwrightNormalizedProducts.Count;
            crawl.SkuCoverage = playwrightResult.SkuCoverage;
            crawl.OemCoverage = playwrightResult.OemCoverage;

            await _externalProductUpsertService.UpsertAsync(crawl.ExternalSiteId, crawl.Id, playwrightNormalizedProducts, cancellationToken);
            await _externalProductUpsertService.MarkMissingInactiveAsync(
                crawl.ExternalSiteId,
                playwrightNormalizedProducts.Select(x => x.Product.SourceUrl).ToArray(),
                cancellationToken);
        }
        else if (fetchNormalizedProducts.Count > 0)
        {
            await _externalProductUpsertService.UpsertAsync(crawl.ExternalSiteId, crawl.Id, fetchNormalizedProducts, cancellationToken);
            await _externalProductUpsertService.MarkMissingInactiveAsync(
                crawl.ExternalSiteId,
                fetchNormalizedProducts.Select(x => x.Product.SourceUrl).ToArray(),
                cancellationToken);
        }

        crawl.Status = needsManagedBrowserFallback ? "partial" : "completed";
        crawl.ErrorSummary = needsManagedBrowserFallback
            ? playwrightResult.Succeeded
                ? $"Playwright tamamlandı ancak managed browser gerekli. productCount={playwrightProductCount}, skuCoverage={playwrightSkuCoverage?.ToString("0.##") ?? "0"}"
                : $"Playwright başarısız. managed browser gerekli. {playwrightResult.ErrorSummary}"
            : null;

        crawl.RawStatsJson = MergeCrawlerStageStats(
            fetchResult.RawStatsJson,
            playwrightResult.RawStatsJson,
            needsManagedBrowserFallback ? "managed_browser" : "none",
            crawl.ProductCount,
            crawl.SkuCoverage);

        if (!needsManagedBrowserFallback)
        {
            crawl.ExternalSite.LastSuccessfulCrawlAtUtc = crawl.CompletedAtUtc;
        }
    }

    private static string MergeCrawlerStageStats(string fetchRawStatsJson, string? playwrightRawStatsJson, string nextStep, int productCount, decimal? skuCoverage)
    {
        var payload = new
        {
            fetch = TryParseJsonElement(fetchRawStatsJson),
            playwright = string.IsNullOrWhiteSpace(playwrightRawStatsJson) ? null : TryParseJsonElement(playwrightRawStatsJson),
            fallback = new
            {
                next = nextStep,
                reason = nextStep == "none"
                    ? null
                    : $"productCount<{MinFetchProductCountForBrowserFallback} veya skuCoverage<{MinFetchSkuCoverageForBrowserFallback}",
                thresholds = new
                {
                    minProductCount = MinFetchProductCountForBrowserFallback,
                    minSkuCoverage = MinFetchSkuCoverageForBrowserFallback
                },
                current = new
                {
                    productCount,
                    skuCoverage = skuCoverage ?? 0
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object? TryParseJsonElement(string rawStatsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawStatsJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return rawStatsJson;
        }
    }
}
