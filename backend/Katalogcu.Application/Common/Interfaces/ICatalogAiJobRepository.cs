using Katalogcu.Domain.Entities;
using Katalogcu.Application.Features.Catalogs.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogAiJobRepository
{
    Task UpsertPendingAsync(Guid catalogId, int maxAttempts, CancellationToken cancellationToken);

    Task<CatalogAiJob?> LeaseDueJobAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task MarkSucceededAsync(Guid jobId, CancellationToken cancellationToken);

    Task MarkRetryAsync(Guid jobId, DateTime nextAttemptAt, string? error, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid jobId, string? error, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogAiJobItemDto>> GetJobsByUserAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<CatalogAiJobSummaryDto> GetJobSummaryByUserAsync(Guid userId, CancellationToken cancellationToken);
}
