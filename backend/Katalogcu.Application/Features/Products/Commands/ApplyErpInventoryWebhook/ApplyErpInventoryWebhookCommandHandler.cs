using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ApplyErpInventoryWebhook;

public sealed class ApplyErpInventoryWebhookCommandHandler
    : IRequestHandler<ApplyErpInventoryWebhookCommand, OperationResult<ApplyErpInventoryWebhookResponse>>
{
    private readonly IErpInventorySnapshotRepository _erpInventorySnapshotRepository;

    public ApplyErpInventoryWebhookCommandHandler(IErpInventorySnapshotRepository erpInventorySnapshotRepository)
    {
        _erpInventorySnapshotRepository = erpInventorySnapshotRepository;
    }

    public async Task<OperationResult<ApplyErpInventoryWebhookResponse>> Handle(
        ApplyErpInventoryWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var updatedProductCount = 0;
        var skippedCount = 0;
        var nowUtc = DateTime.UtcNow;
        var effectiveOccurredAtUtc = request.OccurredAtUtc ?? nowUtc;

        foreach (var item in request.Items)
        {
            var normalizedPartCode = NormalizePartCode(item.PartCode);
            if (!item.ProductId.HasValue &&
                string.IsNullOrWhiteSpace(normalizedPartCode) &&
                string.IsNullOrWhiteSpace(item.ExternalProductId))
            {
                skippedCount++;
                continue;
            }

            var product = await _erpInventorySnapshotRepository.GetOwnedProductAsync(
                request.OwnerUserId,
                item.ProductId,
                normalizedPartCode,
                cancellationToken);

            var snapshot = await _erpInventorySnapshotRepository.GetSnapshotForWebhookAsync(
                request.OwnerUserId,
                request.Provider,
                product?.Id ?? item.ProductId,
                normalizedPartCode ?? product?.Code,
                item.ExternalProductId,
                cancellationToken);

            if (snapshot == null)
            {
                snapshot = new ErpInventorySnapshot
                {
                    Id = Guid.NewGuid(),
                    CreatedDate = nowUtc,
                    OwnerUserId = request.OwnerUserId,
                    Provider = request.Provider
                };
                await _erpInventorySnapshotRepository.AddSnapshotAsync(snapshot, cancellationToken);
            }

            snapshot.ProductId = product?.Id ?? item.ProductId;
            snapshot.Provider = request.Provider;
            snapshot.ExternalProductId = string.IsNullOrWhiteSpace(item.ExternalProductId)
                ? snapshot.ExternalProductId
                : item.ExternalProductId.Trim();
            snapshot.PartCode = normalizedPartCode ?? product?.Code ?? snapshot.PartCode;
            snapshot.ProductName = string.IsNullOrWhiteSpace(item.ProductName)
                ? product?.Name ?? snapshot.ProductName
                : item.ProductName.Trim();
            snapshot.Currency = string.IsNullOrWhiteSpace(item.Currency)
                ? string.IsNullOrWhiteSpace(snapshot.Currency) ? "TRY" : snapshot.Currency
                : item.Currency.Trim().ToUpperInvariant();
            snapshot.IsActive = true;
            snapshot.LastSyncedAtUtc = effectiveOccurredAtUtc;
            snapshot.LastWebhookReceivedAtUtc = nowUtc;
            snapshot.UpdatedDate = nowUtc;

            if (item.UnitPrice.HasValue)
            {
                snapshot.UnitPrice = item.UnitPrice.Value;
            }

            if (item.StockQuantity.HasValue)
            {
                snapshot.AvailableStock = item.StockQuantity.Value;
            }

            if (product != null)
            {
                var productChanged = false;

                if (item.UnitPrice.HasValue && product.Price != item.UnitPrice.Value)
                {
                    product.Price = item.UnitPrice.Value;
                    productChanged = true;
                }

                if (item.StockQuantity.HasValue && product.StockQuantity != item.StockQuantity.Value)
                {
                    var previousQuantity = product.StockQuantity;
                    var newQuantity = item.StockQuantity.Value;
                    product.StockQuantity = newQuantity;
                    productChanged = true;

                    await _erpInventorySnapshotRepository.AddStockMovementAsync(
                        new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            CreatedDate = nowUtc,
                            UserId = request.OwnerUserId,
                            ProductId = product.Id,
                            ProductCode = product.Code,
                            ProductName = product.Name,
                            PreviousQuantity = previousQuantity,
                            DeltaQuantity = newQuantity - previousQuantity,
                            NewQuantity = newQuantity,
                            MovementType = "ERP_SYNC",
                            Reason = string.IsNullOrWhiteSpace(request.EventId)
                                ? $"ERP webhook sync ({request.Provider})"
                                : $"ERP webhook sync ({request.Provider}) Event={request.EventId}",
                            Source = "erp-webhook",
                            ActorName = request.Source,
                            ReferenceId = item.ExternalProductId
                        },
                        cancellationToken);
                }

                if (productChanged)
                {
                    product.UpdatedDate = nowUtc;
                    updatedProductCount++;
                }
            }

            processedCount++;
        }

        await _erpInventorySnapshotRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<ApplyErpInventoryWebhookResponse>.Success(new ApplyErpInventoryWebhookResponse
        {
            ProcessedCount = processedCount,
            UpdatedProductCount = updatedProductCount,
            SkippedCount = skippedCount
        });
    }

    private static string? NormalizePartCode(string? partCode)
    {
        return string.IsNullOrWhiteSpace(partCode)
            ? null
            : partCode.Trim().ToUpperInvariant();
    }
}
