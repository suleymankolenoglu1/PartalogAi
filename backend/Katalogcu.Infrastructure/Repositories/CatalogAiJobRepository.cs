using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Catalogs.Common;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CatalogAiJobRepository : ICatalogAiJobRepository
{
    private readonly AppDbContext _context;

    public CatalogAiJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task UpsertPendingAsync(Guid catalogId, int maxAttempts, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var job = await _context.CatalogAiJobs
            .FirstOrDefaultAsync(x => x.CatalogId == catalogId, cancellationToken);

        if (job == null)
        {
            job = new CatalogAiJob
            {
                CatalogId = catalogId,
                Status = CatalogAiJob.Pending,
                AttemptCount = 0,
                MaxAttempts = maxAttempts,
                NextAttemptAt = now,
                LastAttemptAt = null,
                LockedUntil = null,
                LastError = null,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _context.CatalogAiJobs.AddAsync(job, cancellationToken);
        }
        else
        {
            job.Status = CatalogAiJob.Pending;
            job.AttemptCount = 0;
            job.MaxAttempts = maxAttempts;
            job.NextAttemptAt = now;
            job.LastAttemptAt = null;
            job.LockedUntil = null;
            job.LastError = null;
            job.UpdatedDate = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CatalogAiJob?> LeaseDueJobAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var job = await _context.CatalogAiJobs
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.CreatedDate)
            .FirstOrDefaultAsync(
                x =>
                    x.NextAttemptAt <= utcNow &&
                    (
                        x.Status == CatalogAiJob.Pending ||
                        (x.Status == CatalogAiJob.Processing && x.LockedUntil.HasValue && x.LockedUntil.Value <= utcNow)
                    ),
                cancellationToken);

        if (job == null)
        {
            return null;
        }

        job.Status = CatalogAiJob.Processing;
        job.AttemptCount = Math.Max(0, job.AttemptCount) + 1;
        job.LastAttemptAt = utcNow;
        job.LockedUntil = utcNow.Add(leaseDuration);
        job.UpdatedDate = utcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task MarkSucceededAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _context.CatalogAiJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null)
        {
            return;
        }

        job.Status = CatalogAiJob.Completed;
        job.LockedUntil = null;
        job.LastError = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRetryAsync(Guid jobId, DateTime nextAttemptAt, string? error, CancellationToken cancellationToken)
    {
        var job = await _context.CatalogAiJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null)
        {
            return;
        }

        job.Status = CatalogAiJob.Pending;
        job.NextAttemptAt = nextAttemptAt;
        job.LockedUntil = null;
        job.LastError = TrimError(error);
        job.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string? error, CancellationToken cancellationToken)
    {
        var job = await _context.CatalogAiJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null)
        {
            return;
        }

        job.Status = CatalogAiJob.Failed;
        job.LockedUntil = null;
        job.LastError = TrimError(error);
        job.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogAiJobItemDto>> GetJobsByUserAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);

        return await (
            from job in _context.CatalogAiJobs.AsNoTracking()
            join catalog in _context.Catalogs.AsNoTracking() on job.CatalogId equals catalog.Id
            where catalog.UserId == userId
            orderby (job.UpdatedDate ?? job.CreatedDate) descending
            select new CatalogAiJobItemDto
            {
                JobId = job.Id,
                CatalogId = catalog.Id,
                CatalogName = catalog.Name,
                Status = job.Status,
                AttemptCount = job.AttemptCount,
                MaxAttempts = job.MaxAttempts,
                NextAttemptAt = job.NextAttemptAt,
                LastAttemptAt = job.LastAttemptAt,
                LockedUntil = job.LockedUntil,
                LastError = job.LastError,
                CreatedDate = job.CreatedDate,
                UpdatedDate = job.UpdatedDate
            })
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogAiJobSummaryDto> GetJobSummaryByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var statusCounts = await (
            from job in _context.CatalogAiJobs.AsNoTracking()
            join catalog in _context.Catalogs.AsNoTracking() on job.CatalogId equals catalog.Id
            where catalog.UserId == userId
            group job by job.Status into g
            select new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pending = 0;
        var processing = 0;
        var completed = 0;
        var failed = 0;

        foreach (var item in statusCounts)
        {
            if (string.Equals(item.Status, CatalogAiJob.Pending, StringComparison.OrdinalIgnoreCase))
            {
                pending = item.Count;
                continue;
            }

            if (string.Equals(item.Status, CatalogAiJob.Processing, StringComparison.OrdinalIgnoreCase))
            {
                processing = item.Count;
                continue;
            }

            if (string.Equals(item.Status, CatalogAiJob.Completed, StringComparison.OrdinalIgnoreCase))
            {
                completed = item.Count;
                continue;
            }

            if (string.Equals(item.Status, CatalogAiJob.Failed, StringComparison.OrdinalIgnoreCase))
            {
                failed = item.Count;
            }
        }

        return new CatalogAiJobSummaryDto
        {
            Total = pending + processing + completed + failed,
            Pending = pending,
            Processing = processing,
            Completed = completed,
            Failed = failed
        };
    }

    private static string? TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        return error.Length <= 2048 ? error : error[..2048];
    }
}
