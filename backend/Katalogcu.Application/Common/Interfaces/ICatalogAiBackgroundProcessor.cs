namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogAiBackgroundProcessor
{
    Task EnqueueAsync(Guid catalogId, CancellationToken cancellationToken);
}
