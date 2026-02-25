using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class CatalogAiJob : BaseEntity
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public Guid CatalogId { get; set; }
    public Catalog? Catalog { get; set; }

    public string Status { get; set; } = Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;

    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }

    public string? LastError { get; set; }
}
