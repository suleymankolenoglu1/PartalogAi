using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalSitePlaywrightCrawler : IExternalSitePlaywrightCrawler
{
    private readonly ILogger<ExternalSitePlaywrightCrawler> _logger;

    public ExternalSitePlaywrightCrawler(ILogger<ExternalSitePlaywrightCrawler> logger)
    {
        _logger = logger;
    }

    public Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken)
    {
        const string reason = "Playwright crawler bu build'de kapalı. Etkinleştirmek için /p:EnablePlaywrightCrawler=true ile build alın.";
        _logger.LogInformation("External site Playwright crawler disabled: {BaseUrl}", baseUrl);

        return Task.FromResult(new ExternalSiteFetchResult
        {
            Succeeded = false,
            ErrorSummary = reason,
            RawStatsJson = JsonSerializer.Serialize(new
            {
                fetchedBy = "playwright-disabled",
                error = reason,
                url = baseUrl
            })
        });
    }
}
