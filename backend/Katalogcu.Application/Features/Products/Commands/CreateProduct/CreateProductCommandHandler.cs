using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, OperationResult<CreateProductResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public CreateProductCommandHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<CreateProductResponse>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<CreateProductResponse>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        if (request.CatalogId != Guid.Empty)
        {
            var ownsCatalog = await _stockRepository.UserOwnsCatalogAsync(_currentUser.UserId, request.CatalogId, cancellationToken);
            if (!ownsCatalog)
            {
                return OperationResult<CreateProductResponse>.Failure("not_found", "Seçilen katalog size ait değil veya bulunamadı.");
            }
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CatalogId = request.CatalogId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            OemNo = NullIfEmpty(request.OemNo),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            ImageUrl = NullIfEmpty(request.ImageUrl),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Genel" : request.Category.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            PageNumber = request.PageNumber?.Trim() ?? string.Empty,
            RefNo = request.RefNo,
            CreatedDate = now
        };

        await _stockRepository.AddProductAsync(product, cancellationToken);
        await _stockRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<CreateProductResponse>.Success(new CreateProductResponse
        {
            Id = product.Id,
            CatalogId = product.CatalogId,
            Name = product.Name,
            Code = product.Code,
            OemNo = product.OemNo,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ImageUrl = product.ImageUrl,
            Category = product.Category,
            Description = product.Description,
            PageNumber = product.PageNumber,
            RefNo = product.RefNo,
            CreatedDate = product.CreatedDate
        });
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
