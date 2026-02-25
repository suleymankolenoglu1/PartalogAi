using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed class CatalogAiOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogAiOutboxWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _leaseDuration;
    private readonly TimeSpan _baseRetryDelay;

    public CatalogAiOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CatalogAiOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var pollSeconds = configuration.GetValue<int?>("CatalogAiProcessing:PollIntervalSeconds") ?? 2;
        var leaseSeconds = configuration.GetValue<int?>("CatalogAiProcessing:LeaseSeconds") ?? 300;
        var baseRetryDelaySeconds = configuration.GetValue<int?>("CatalogAiProcessing:BaseRetryDelaySeconds") ?? 2;

        _pollInterval = TimeSpan.FromSeconds(Math.Clamp(pollSeconds, 1, 30));
        _leaseDuration = TimeSpan.FromSeconds(Math.Clamp(leaseSeconds, 30, 1800));
        _baseRetryDelay = TimeSpan.FromSeconds(Math.Clamp(baseRetryDelaySeconds, 1, 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Catalog AI outbox worker started. Poll={PollSec}s Lease={LeaseSec}s BaseRetryDelay={BaseDelaySec}s",
            _pollInterval.TotalSeconds,
            _leaseDuration.TotalSeconds,
            _baseRetryDelay.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            LeasedJob? leasedJob = null;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ICatalogAiJobRepository>();
                var processor = scope.ServiceProvider.GetRequiredService<CatalogProcessorService>();

                var job = await repo.LeaseDueJobAsync(DateTime.UtcNow, _leaseDuration, stoppingToken);
                if (job == null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                leasedJob = new LeasedJob(job.Id, job.CatalogId, job.AttemptCount, job.MaxAttempts);

                _logger.LogInformation(
                    "Catalog AI outbox job leased: JobId={JobId} CatalogId={CatalogId} Attempt={Attempt}/{MaxAttempts}",
                    leasedJob.JobId,
                    leasedJob.CatalogId,
                    leasedJob.AttemptCount,
                    leasedJob.MaxAttempts);

                var succeeded = await processor.ProcessCatalogAsync(leasedJob.CatalogId, stoppingToken);
                if (succeeded)
                {
                    await repo.MarkSucceededAsync(leasedJob.JobId, stoppingToken);
                    _logger.LogInformation("Catalog AI outbox job completed: JobId={JobId}", leasedJob.JobId);
                    continue;
                }

                await HandleFailureAsync(scope.ServiceProvider, leasedJob, "ProcessCatalogAsync returned false", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog AI outbox worker error.");

                if (leasedJob != null)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await HandleFailureAsync(scope.ServiceProvider, leasedJob, ex.Message, stoppingToken);
                    }
                    catch (Exception markEx)
                    {
                        _logger.LogError(markEx, "Catalog AI outbox failure handling error: JobId={JobId}", leasedJob.JobId);
                    }
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }

    private async Task HandleFailureAsync(IServiceProvider serviceProvider, LeasedJob leasedJob, string error, CancellationToken cancellationToken)
    {
        var repo = serviceProvider.GetRequiredService<ICatalogAiJobRepository>();

        if (leasedJob.AttemptCount >= leasedJob.MaxAttempts)
        {
            await repo.MarkFailedAsync(leasedJob.JobId, error, cancellationToken);
            await MarkCatalogAsErrorAsync(serviceProvider, leasedJob.CatalogId, cancellationToken);

            _logger.LogError(
                "Catalog AI outbox job failed permanently: JobId={JobId} CatalogId={CatalogId} Attempts={AttemptCount}",
                leasedJob.JobId,
                leasedJob.CatalogId,
                leasedJob.AttemptCount);

            return;
        }

        var nextAttemptAt = DateTime.UtcNow.Add(GetRetryDelay(leasedJob.AttemptCount));
        await repo.MarkRetryAsync(leasedJob.JobId, nextAttemptAt, error, cancellationToken);

        _logger.LogWarning(
            "Catalog AI outbox job scheduled for retry: JobId={JobId} CatalogId={CatalogId} NextAttemptAt={NextAttemptAt:O} Attempt={Attempt}/{MaxAttempts}",
            leasedJob.JobId,
            leasedJob.CatalogId,
            nextAttemptAt,
            leasedJob.AttemptCount,
            leasedJob.MaxAttempts);
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var multiplier = Math.Pow(2, Math.Max(0, attemptCount - 1));
        var delayMs = _baseRetryDelay.TotalMilliseconds * multiplier;
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, TimeSpan.FromMinutes(2).TotalMilliseconds));
    }

    private static async Task MarkCatalogAsErrorAsync(IServiceProvider serviceProvider, Guid catalogId, CancellationToken cancellationToken)
    {
        var db = serviceProvider.GetRequiredService<AppDbContext>();
        var catalog = await db.Catalogs.FirstOrDefaultAsync(c => c.Id == catalogId, cancellationToken);
        if (catalog == null)
        {
            return;
        }

        catalog.Status = "Error";
        catalog.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record LeasedJob(Guid JobId, Guid CatalogId, int AttemptCount, int MaxAttempts);
}
