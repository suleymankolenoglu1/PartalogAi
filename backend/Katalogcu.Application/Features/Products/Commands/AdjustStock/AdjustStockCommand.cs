using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.AdjustStock;

public sealed record AdjustStockCommand(Guid ProductId, int DeltaQuantity, string? Reason)
    : IRequest<OperationResult<AdjustStockResponse>>;
