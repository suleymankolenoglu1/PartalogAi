using Hangfire;
using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class CatalogAiHangfireJob
{
    public const string QueueName = "catalog-ai";

    private readonly CatalogProcessorService _catalogProcessorService;
    private readonly ICatalogAiJobRepository _catalogAiJobRepository;
    private readonly ILogger<CatalogAiHangfireJob> _logger;

    public CatalogAiHangfireJob(
        CatalogProcessorService catalogProcessorService,
        ICatalogAiJobRepository catalogAiJobRepository,
        ILogger<CatalogAiHangfireJob> logger)
    {
        _catalogProcessorService = catalogProcessorService;
        _catalogAiJobRepository = catalogAiJobRepository;
        _logger = logger;
    }

    [Queue(QueueName)]
    public async Task ExecuteAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Catalog AI Hangfire job başladı: {CatalogId}", catalogId);

        var succeeded = await _catalogProcessorService.ProcessCatalogAsync(catalogId, cancellationToken);
        if (!succeeded)
        {
            throw new InvalidOperationException($"Catalog processing returned failure for catalog {catalogId}.");
        }

        await _catalogAiJobRepository.MarkSucceededAsync(catalogId, cancellationToken);

        _logger.LogInformation("Catalog AI Hangfire job tamamlandı: {CatalogId}", catalogId);
    }
}
