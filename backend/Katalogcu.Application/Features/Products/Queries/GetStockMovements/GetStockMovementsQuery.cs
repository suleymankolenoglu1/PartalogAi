using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Queries.GetStockMovements;

public sealed record GetStockMovementsQuery(Guid? ProductId, int Limit)
    : IRequest<OperationResult<IReadOnlyList<StockMovementDto>>>;
