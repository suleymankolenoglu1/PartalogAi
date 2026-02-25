using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetStockMovements;

public sealed class GetStockMovementsQueryHandler : IRequestHandler<GetStockMovementsQuery, OperationResult<IReadOnlyList<StockMovementDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IStockRepository _stockRepository;

    public GetStockMovementsQueryHandler(ICurrentUserService currentUser, IStockRepository stockRepository)
    {
        _currentUser = currentUser;
        _stockRepository = stockRepository;
    }

    public async Task<OperationResult<IReadOnlyList<StockMovementDto>>> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<StockMovementDto>>.Failure("unauthorized", "Kullanıcı doğrulanamadı.");
        }

        var rows = await _stockRepository.GetStockMovementsAsync(
            _currentUser.UserId,
            request.ProductId,
            request.Limit,
            cancellationToken);

        var result = rows
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductCode = m.ProductCode,
                ProductName = m.ProductName,
                PreviousQuantity = m.PreviousQuantity,
                DeltaQuantity = m.DeltaQuantity,
                NewQuantity = m.NewQuantity,
                MovementType = m.MovementType,
                Reason = m.Reason,
                Source = m.Source,
                ActorName = m.ActorName,
                ReferenceId = m.ReferenceId,
                CreatedDate = m.CreatedDate
            })
            .ToList();

        return OperationResult<IReadOnlyList<StockMovementDto>>.Success(result);
    }
}
