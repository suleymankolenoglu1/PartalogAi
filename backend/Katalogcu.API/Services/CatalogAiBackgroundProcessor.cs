using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class CatalogAiBackgroundProcessor : ICatalogAiBackgroundProcessor
{
    private readonly ICatalogAiJobRepository _catalogAiJobRepository;
    private readonly ILogger<CatalogAiBackgroundProcessor> _logger;
    private readonly int _maxAttempts;

    public CatalogAiBackgroundProcessor(
        ICatalogAiJobRepository catalogAiJobRepository,
        IConfiguration configuration,
        ILogger<CatalogAiBackgroundProcessor> logger)
    {
        _catalogAiJobRepository = catalogAiJobRepository;
        _logger = logger;

        var configuredMaxAttempts = configuration.GetValue<int?>("CatalogAiProcessing:MaxAttempts") ?? 3;
        _maxAttempts = Math.Clamp(configuredMaxAttempts, 1, 10);
    }

    public async Task EnqueueAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        await _catalogAiJobRepository.UpsertPendingAsync(catalogId, _maxAttempts, cancellationToken);

        _logger.LogInformation(
            "Catalog AI outbox kaydı eklendi: {CatalogId} | MaxAttempts={MaxAttempts}",
            catalogId,
            _maxAttempts);
    }
}
