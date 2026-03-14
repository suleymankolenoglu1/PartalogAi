using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class ErpInventorySnapshotRepository : IErpInventorySnapshotRepository
{
    private readonly AppDbContext _context;

    public ErpInventorySnapshotRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ErpInventorySnapshot?> GetSnapshotAsync(
        Guid ownerUserId,
        Guid? productId,
        string? partCode,
        string? preferredProvider,
        CancellationToken cancellationToken)
    {
        var normalizedPartCode = NormalizePartCode(partCode);

        var query = _context.ErpInventorySnapshots
            .AsNoTracking()
            .Where(x => x.OwnerUserId == ownerUserId && x.IsActive);

        if (!string.IsNullOrWhiteSpace(preferredProvider))
        {
            query = query.Where(x => x.Provider == preferredProvider);
        }

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedPartCode))
        {
            query = query.Where(x => x.PartCode == normalizedPartCode);
        }
        else
        {
            return Task.FromResult<ErpInventorySnapshot?>(null);
        }

        return query
            .OrderByDescending(x => x.LastSyncedAtUtc)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ErpInventorySnapshot?> GetSnapshotForWebhookAsync(
        Guid ownerUserId,
        string provider,
        Guid? productId,
        string? partCode,
        string? externalProductId,
        CancellationToken cancellationToken)
    {
        var normalizedPartCode = NormalizePartCode(partCode);
        var trimmedExternalProductId = string.IsNullOrWhiteSpace(externalProductId)
            ? null
            : externalProductId.Trim();

        var query = _context.ErpInventorySnapshots.Where(x =>
            x.OwnerUserId == ownerUserId &&
            x.Provider == provider);

        if (!string.IsNullOrWhiteSpace(trimmedExternalProductId))
        {
            return query.FirstOrDefaultAsync(x => x.ExternalProductId == trimmedExternalProductId, cancellationToken);
        }

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            return query.FirstOrDefaultAsync(x => x.ProductId == productId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPartCode))
        {
            return query.FirstOrDefaultAsync(x => x.PartCode == normalizedPartCode, cancellationToken);
        }

        return Task.FromResult<ErpInventorySnapshot?>(null);
    }

    public Task<Product?> GetOwnedProductAsync(
        Guid ownerUserId,
        Guid? productId,
        string? partCode,
        CancellationToken cancellationToken)
    {
        var normalizedPartCode = NormalizePartCode(partCode);

        var query = _context.Products
            .Include(x => x.Catalog)
            .Where(x => x.Catalog != null && x.Catalog.UserId == ownerUserId);

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            return query.FirstOrDefaultAsync(x => x.Id == productId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPartCode))
        {
            return query
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync(x => x.Code == normalizedPartCode, cancellationToken);
        }

        return Task.FromResult<Product?>(null);
    }

    public Task AddSnapshotAsync(ErpInventorySnapshot snapshot, CancellationToken cancellationToken)
    {
        return _context.ErpInventorySnapshots.AddAsync(snapshot, cancellationToken).AsTask();
    }

    public Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        return _context.StockMovements.AddAsync(movement, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizePartCode(string? partCode)
    {
        return string.IsNullOrWhiteSpace(partCode)
            ? null
            : partCode.Trim().ToUpperInvariant();
    }
}
