using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class ErpInventorySnapshot : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string? ExternalProductId { get; set; }
    public string PartCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? UnitPrice { get; set; }
    public int? AvailableStock { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastWebhookReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
