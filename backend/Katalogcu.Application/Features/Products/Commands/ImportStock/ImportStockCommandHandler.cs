using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ImportStock;

public sealed class ImportStockCommandHandler : IRequestHandler<ImportStockCommand, OperationResult<ImportStockResponse>>
{
    private const int MaxStockQuantity = 1_000_000;
    private const decimal MaxPrice = 10_000_000m;
    private const int MaxCodeLength = 64;
    private const int MaxNameLength = 200;
    private const int MaxCategoryLength = 120;
    private const int MaxDescriptionLength = 4000;

    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public ImportStockCommandHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<ImportStockResponse>> Handle(ImportStockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ImportStockResponse>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var allowUpsert = string.Equals(request.Mode, "upsert", StringComparison.OrdinalIgnoreCase);
        var selectedCatalogId = request.CatalogId.HasValue && request.CatalogId.Value != Guid.Empty
            ? request.CatalogId.Value
            : (Guid?)null;

        if (selectedCatalogId.HasValue)
        {
            var ownsCatalog = await _stockRepository.UserOwnsCatalogAsync(_currentUser.UserId, selectedCatalogId.Value, cancellationToken);
            if (!ownsCatalog)
            {
                return OperationResult<ImportStockResponse>.Failure("not_found", "Seçilen katalog size ait değil.");
            }
        }

        var existingProducts = await _stockRepository.GetOwnedProductsAsync(
            _currentUser.UserId,
            selectedCatalogId,
            cancellationToken);

        var codeMap = existingProducts
            .GroupBy(p => NormalizeCode(p.Code))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.ToList());

        var updated = 0;
        var created = 0;
        var skipped = 0;
        var skippedRows = new List<ImportStockSkippedRow>();
        var movementLogs = new List<StockMovement>();
        var importBatchId = Guid.NewGuid().ToString("N");
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in request.Rows)
        {
            var codeKey = NormalizeCode(row.Code);
            if (string.IsNullOrWhiteSpace(codeKey))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Parça kodu boş."));
                continue;
            }

            if (!seenCodes.Add(codeKey))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Dosyada aynı kod birden fazla kez geçiyor."));
                continue;
            }

            if (codeKey.Length > MaxCodeLength)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Parça kodu en fazla {MaxCodeLength} karakter olabilir."));
                continue;
            }

            if (!row.StockQuantity.HasValue)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Stok adedi sayısal değil."));
                continue;
            }

            if (row.StockQuantity.Value < 0)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Negatif stok desteklenmiyor."));
                continue;
            }

            if (row.StockQuantity.Value > MaxStockQuantity)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Stok adedi en fazla {MaxStockQuantity} olabilir."));
                continue;
            }

            if (row.Price.HasValue && (row.Price.Value < 0 || row.Price.Value > MaxPrice))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Fiyat 0 ile {MaxPrice} arasında olmalı."));
                continue;
            }

            if (!IsWithinLimit(row.Name, MaxNameLength))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Ürün adı en fazla {MaxNameLength} karakter olabilir."));
                continue;
            }

            if (!IsWithinLimit(row.Category, MaxCategoryLength))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Kategori en fazla {MaxCategoryLength} karakter olabilir."));
                continue;
            }

            if (!IsWithinLimit(row.Description, MaxDescriptionLength))
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, $"Açıklama en fazla {MaxDescriptionLength} karakter olabilir."));
                continue;
            }

            if (codeMap.TryGetValue(codeKey, out var matches))
            {
                if (matches.Count > 1)
                {
                    skipped++;
                    skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Aynı koddan birden fazla ürün var. Katalog filtresiyle tekrar deneyin."));
                    continue;
                }

                var product = matches[0];
                var previousQuantity = product.StockQuantity;
                product.StockQuantity = row.StockQuantity.Value;
                if (row.Price.HasValue) product.Price = row.Price.Value;
                if (!string.IsNullOrWhiteSpace(row.Name)) product.Name = row.Name.Trim();
                if (!string.IsNullOrWhiteSpace(row.Category)) product.Category = row.Category.Trim();
                if (row.Description is not null) product.Description = row.Description.Trim();
                product.UpdatedDate = DateTime.UtcNow;

                if (previousQuantity != product.StockQuantity)
                {
                    movementLogs.Add(BuildStockMovement(
                        userId: _currentUser.UserId,
                        product: product,
                        previousQuantity: previousQuantity,
                        newQuantity: product.StockQuantity,
                        movementType: "IMPORT",
                        reason: $"Stok import satırı #{row.RowNumber}",
                        source: "products/import-stock",
                        actorName: _currentUser.ActorName,
                        referenceId: importBatchId));
                }

                updated++;
                continue;
            }

            if (!allowUpsert)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Ürün bulunamadı (mode=update_only)."));
                continue;
            }

            if (!selectedCatalogId.HasValue)
            {
                skipped++;
                skippedRows.Add(new ImportStockSkippedRow(row.RowNumber, row.Code, "Yeni ürün için katalog seçilmedi."));
                continue;
            }

            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                CatalogId = selectedCatalogId.Value,
                Code = row.Code.Trim(),
                Name = !string.IsNullOrWhiteSpace(row.Name) ? row.Name.Trim() : $"Parça {row.Code.Trim()}",
                Category = !string.IsNullOrWhiteSpace(row.Category) ? row.Category.Trim() : "Genel",
                Description = row.Description?.Trim() ?? string.Empty,
                Price = row.Price ?? 0,
                StockQuantity = row.StockQuantity.Value,
                CreatedDate = DateTime.UtcNow
            };

            await _stockRepository.AddProductAsync(newProduct, cancellationToken);
            created++;

            movementLogs.Add(BuildStockMovement(
                userId: _currentUser.UserId,
                product: newProduct,
                previousQuantity: 0,
                newQuantity: newProduct.StockQuantity,
                movementType: "IMPORT",
                reason: $"Import ile yeni ürün oluşturuldu (satır #{row.RowNumber})",
                source: "products/import-stock",
                actorName: _currentUser.ActorName,
                referenceId: importBatchId));

            codeMap[codeKey] = [newProduct];
        }

        if (movementLogs.Count > 0)
        {
            await _stockRepository.AddStockMovementsAsync(movementLogs, cancellationToken);
        }

        await _stockRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<ImportStockResponse>.Success(new ImportStockResponse
        {
            TotalRows = request.Rows.Count,
            Updated = updated,
            Created = created,
            Skipped = skipped,
            Mode = allowUpsert ? "upsert" : "update_only",
            SkippedRows = skippedRows
        });
    }

    private static string NormalizeCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static bool IsWithinLimit(string? value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;
    }

    private static StockMovement BuildStockMovement(
        Guid userId,
        Product product,
        int previousQuantity,
        int newQuantity,
        string movementType,
        string reason,
        string source,
        string actorName,
        string? referenceId)
    {
        return new StockMovement
        {
            Id = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            UserId = userId,
            ProductId = product.Id,
            ProductCode = product.Code ?? string.Empty,
            ProductName = product.Name ?? string.Empty,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            DeltaQuantity = newQuantity - previousQuantity,
            MovementType = movementType,
            Reason = string.IsNullOrWhiteSpace(reason) ? "-" : reason.Trim(),
            Source = source,
            ActorName = actorName,
            ReferenceId = referenceId
        };
    }
}
