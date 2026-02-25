using Katalogcu.Application.Features.Catalogs.Commands.ProcessCatalogPages;
using MediatR;

namespace Katalogcu.API.Services;

public sealed class CatalogProcessorService
{
    private readonly ISender _sender;
    private readonly ILogger<CatalogProcessorService> _logger;

    public CatalogProcessorService(ISender sender, ILogger<CatalogProcessorService> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task<bool> ProcessCatalogAsync(Guid catalogId, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ProcessCatalogPagesCommand(catalogId), cancellationToken);
        if (result.IsSuccess)
        {
            return true;
        }

        _logger.LogWarning(
            "Katalog işleme use-case başarısız: {CatalogId} | {ErrorCode} | {ErrorMessage}",
            catalogId,
            result.ErrorCode,
            result.ErrorMessage);

        return false;
    }
}
