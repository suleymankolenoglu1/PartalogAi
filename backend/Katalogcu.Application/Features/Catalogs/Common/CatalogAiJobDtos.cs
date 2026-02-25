namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class CatalogAiJobItemDto
{
    public Guid JobId { get; init; }
    public Guid CatalogId { get; init; }
    public string CatalogName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public DateTime? LockedUntil { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class CatalogAiJobSummaryDto
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Processing { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
}

public sealed class CatalogAiJobsDto
{
    public CatalogAiJobSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<CatalogAiJobItemDto> Jobs { get; init; } = [];
}
