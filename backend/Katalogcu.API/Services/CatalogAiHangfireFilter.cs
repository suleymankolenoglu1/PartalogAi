using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Katalogcu.Application.Common.Exceptions;
using Katalogcu.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class CatalogAiHangfireFilter : JobFilterAttribute, IServerFilter, IElectStateFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogAiHangfireFilter> _logger;
    private readonly CatalogAiProcessingOptions _options;

    public CatalogAiHangfireFilter(
        IServiceScopeFactory scopeFactory,
        IOptions<CatalogAiProcessingOptions> options,
        ILogger<CatalogAiHangfireFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        if (!TryGetCatalogId(filterContext.BackgroundJob.Job, out var catalogId))
        {
            return;
        }

        var attemptNumber = GetAttemptNumber(filterContext);
        ExecuteScoped(async serviceProvider =>
        {
            var repo = serviceProvider.GetRequiredService<ICatalogAiJobRepository>();
            await repo.MarkProcessingAsync(catalogId, attemptNumber, CancellationToken.None);
        });

        _logger.LogInformation(
            "Catalog AI Hangfire attempt başladı: {CatalogId} | Attempt={Attempt}/{MaxAttempts}",
            catalogId,
            attemptNumber,
            _options.GetNormalizedMaxAttempts());
    }

    public void OnPerformed(PerformedContext filterContext)
    {
    }

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is not FailedState failedState)
        {
            return;
        }

        if (!TryGetCatalogId(context.BackgroundJob.Job, out var catalogId))
        {
            return;
        }

        var attemptNumber = GetAttemptNumber(context);
        var maxAttempts = _options.GetNormalizedMaxAttempts();
        var error = failedState.Exception.Message;

        if (failedState.Exception is CatalogAiRetryableException retryableException &&
            attemptNumber < maxAttempts)
        {
            var delay = _options.GetRetryDelay(attemptNumber);
            var nextAttemptAt = DateTime.UtcNow.Add(delay);

            ExecuteScoped(async serviceProvider =>
            {
                var repo = serviceProvider.GetRequiredService<ICatalogAiJobRepository>();
                await repo.MarkRetryAsync(catalogId, attemptNumber, nextAttemptAt, error, CancellationToken.None);
            });

            context.SetJobParameter("RetryCount", attemptNumber);
            context.CandidateState = new ScheduledState(delay);

            _logger.LogWarning(
                failedState.Exception,
                "Catalog AI Hangfire retry planlandı: {CatalogId} | Attempt={Attempt}/{MaxAttempts} | NextAttemptAt={NextAttemptAt:O} | Operation={Operation}",
                catalogId,
                attemptNumber,
                maxAttempts,
                nextAttemptAt,
                retryableException.Operation);

            return;
        }

        ExecuteScoped(async serviceProvider =>
        {
            var repo = serviceProvider.GetRequiredService<ICatalogAiJobRepository>();
            var catalogRepo = serviceProvider.GetRequiredService<ICatalogProcessingRepository>();

            await repo.MarkFailedAsync(catalogId, attemptNumber, error, CancellationToken.None);

            var catalog = await catalogRepo.GetCatalogForProcessingAsync(catalogId, CancellationToken.None);
            if (catalog != null)
            {
                catalog.Status = "Error";
                catalog.UpdatedDate = DateTime.UtcNow;
                await catalogRepo.SaveChangesAsync(CancellationToken.None);
            }
        });

        _logger.LogError(
            failedState.Exception,
            "Catalog AI Hangfire job kalıcı olarak başarısız oldu: {CatalogId} | Attempt={Attempt}/{MaxAttempts}",
            catalogId,
            attemptNumber,
            maxAttempts);
    }

    private void ExecuteScoped(Func<IServiceProvider, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        action(scope.ServiceProvider).GetAwaiter().GetResult();
    }

    private static int GetAttemptNumber(PerformingContext context)
    {
        try
        {
            return context.GetJobParameter<int>("RetryCount") + 1;
        }
        catch
        {
            return 1;
        }
    }

    private static int GetAttemptNumber(ElectStateContext context)
    {
        try
        {
            return context.GetJobParameter<int>("RetryCount") + 1;
        }
        catch
        {
            return 1;
        }
    }

    private static bool TryGetCatalogId(Job job, out Guid catalogId)
    {
        catalogId = Guid.Empty;

        if (job.Method.DeclaringType != typeof(CatalogAiHangfireJob))
        {
            return false;
        }

        if (job.Args.Count == 0)
        {
            return false;
        }

        if (job.Args[0] is Guid guid)
        {
            catalogId = guid;
            return true;
        }

        if (job.Args[0] is string value && Guid.TryParse(value, out guid))
        {
            catalogId = guid;
            return true;
        }

        return false;
    }
}
