namespace Katalogcu.Application.Common.Models;

public sealed class ErpProductAvailabilityRequest
{
    public Guid OwnerUserId { get; init; }
    public Guid? ProductId { get; init; }
    public string? PartCode { get; init; }
    public int RequestedQuantity { get; init; } = 1;
    public string? PreferredProvider { get; init; }
}

public sealed class ErpProductAvailabilityResult
{
    public Guid? ProductId { get; init; }
    public string PartCode { get; init; } = string.Empty;
    public string? ProductName { get; init; }
    public decimal? UnitPrice { get; init; }
    public int? AvailableStock { get; init; }
    public bool IsAvailable { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Currency { get; init; } = "TRY";
    public string? ExternalProductId { get; init; }
    public DateTime? SynchronizedAtUtc { get; init; }
}
