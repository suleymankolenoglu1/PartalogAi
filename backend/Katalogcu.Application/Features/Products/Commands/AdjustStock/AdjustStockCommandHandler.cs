using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.AdjustStock;

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, OperationResult<AdjustStockResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public AdjustStockCommandHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<AdjustStockResponse>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<AdjustStockResponse>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var product = await _stockRepository.GetOwnedProductAsync(request.ProductId, _currentUser.UserId, cancellationToken);
        if (product == null)
        {
            return OperationResult<AdjustStockResponse>.Failure("not_found", "Ürün bulunamadı veya yetkiniz yok.");
        }

        var previousQuantity = product.StockQuantity;
        var newQuantity = previousQuantity + request.DeltaQuantity;
        if (newQuantity < 0)
        {
            return OperationResult<AdjustStockResponse>.Failure("validation", "Stok eksiye düşemez.");
        }

        product.StockQuantity = newQuantity;
        product.UpdatedDate = DateTime.UtcNow;

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            PreviousQuantity = previousQuantity,
            DeltaQuantity = request.DeltaQuantity,
            NewQuantity = newQuantity,
            MovementType = "ADJUSTMENT",
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manuel stok düzeltmesi" : request.Reason.Trim(),
            Source = "dashboard/parts",
            ActorName = _currentUser.ActorName
        };

        await _stockRepository.AddStockMovementAsync(movement, cancellationToken);
        await _stockRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<AdjustStockResponse>.Success(new AdjustStockResponse
        {
            ProductId = product.Id,
            Code = product.Code,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            Delta = request.DeltaQuantity
        });
    }
}
