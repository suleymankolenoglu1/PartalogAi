using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ImportStock;

public sealed record ImportStockCommand(
    Guid? CatalogId,
    string Mode,
    IReadOnlyList<ImportStockRowInput> Rows)
    : IRequest<OperationResult<ImportStockResponse>>;

public sealed class ImportStockRowInput
{
    public int RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public int? StockQuantity { get; init; }
    public decimal? Price { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
}
