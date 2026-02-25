using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ImportProducts;

public sealed class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, OperationResult<ImportProductsResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public ImportProductsCommandHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<ImportProductsResponse>> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ImportProductsResponse>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var selectedCatalogId = request.CatalogId.HasValue && request.CatalogId.Value != Guid.Empty
            ? request.CatalogId.Value
            : (Guid?)null;

        if (selectedCatalogId.HasValue)
        {
            var ownsCatalog = await _stockRepository.UserOwnsCatalogAsync(_currentUser.UserId, selectedCatalogId.Value, cancellationToken);
            if (!ownsCatalog)
            {
                return OperationResult<ImportProductsResponse>.Failure("not_found", "Seçilen katalog size ait değil.");
            }
        }

        var products = request.Rows.Select(row => new Product
        {
            Id = Guid.NewGuid(),
            CatalogId = selectedCatalogId ?? Guid.Empty,
            Name = row.Name.Trim(),
            Code = row.Code.Trim(),
            Category = row.Category?.Trim() ?? string.Empty,
            Price = row.Price,
            StockQuantity = row.StockQuantity,
            Description = row.Description?.Trim() ?? string.Empty,
            CreatedDate = DateTime.UtcNow
        }).ToList();

        await _stockRepository.AddProductsAsync(products, cancellationToken);
        await _stockRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<ImportProductsResponse>.Success(new ImportProductsResponse
        {
            Count = products.Count
        });
    }
}
