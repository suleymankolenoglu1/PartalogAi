using Katalogcu.Domain.Entities;
using Katalogcu.Application.Features.Catalogs.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogAiJobRepository
{
    Task UpsertPendingAsync(Guid catalogId, int maxAttempts, CancellationToken cancellationToken);

    Task MarkProcessingAsync(Guid catalogId, int attemptCount, CancellationToken cancellationToken);

    Task MarkSucceededAsync(Guid catalogId, CancellationToken cancellationToken);

    Task MarkRetryAsync(Guid catalogId, int attemptCount, DateTime nextAttemptAt, string? error, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid catalogId, int attemptCount, string? error, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogAiJobItemDto>> GetJobsByUserAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<CatalogAiJobSummaryDto> GetJobSummaryByUserAsync(Guid userId, CancellationToken cancellationToken);
}
