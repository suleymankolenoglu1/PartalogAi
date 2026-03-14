using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IErpInventorySnapshotRepository
{
    Task<ErpInventorySnapshot?> GetSnapshotAsync(
        Guid ownerUserId,
        Guid? productId,
        string? partCode,
        string? preferredProvider,
        CancellationToken cancellationToken);

    Task<ErpInventorySnapshot?> GetSnapshotForWebhookAsync(
        Guid ownerUserId,
        string provider,
        Guid? productId,
        string? partCode,
        string? externalProductId,
        CancellationToken cancellationToken);

    Task<Product?> GetOwnedProductAsync(
        Guid ownerUserId,
        Guid? productId,
        string? partCode,
        CancellationToken cancellationToken);

    Task AddSnapshotAsync(ErpInventorySnapshot snapshot, CancellationToken cancellationToken);

    Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
