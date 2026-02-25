using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, OperationResult<bool>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public DeleteProductCommandHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<bool>> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<bool>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var product = await _stockRepository.GetProductWithCatalogAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return OperationResult<bool>.Failure("not_found", "Ürün bulunamadı.");
        }

        if (product.Catalog != null && product.Catalog.UserId != _currentUser.UserId)
        {
            return OperationResult<bool>.Failure("unauthorized", "Bu ürünü silme yetkiniz yok.");
        }

        var linkedHotspots = await _stockRepository.GetHotspotsByProductIdAsync(request.ProductId, cancellationToken);
        if (linkedHotspots.Count > 0)
        {
            _stockRepository.RemoveHotspots(linkedHotspots);
        }

        var orderItems = await _stockRepository.GetOrderItemsByProductIdAsync(request.ProductId, cancellationToken);
        if (orderItems.Count > 0)
        {
            _stockRepository.RemoveOrderItems(orderItems);
        }

        _stockRepository.RemoveProduct(product);
        await _stockRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}
