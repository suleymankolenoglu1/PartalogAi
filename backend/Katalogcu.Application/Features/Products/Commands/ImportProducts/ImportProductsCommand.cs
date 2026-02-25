using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.ImportProducts;

public sealed record ImportProductsCommand(
    Guid? CatalogId,
    IReadOnlyList<ImportProductRowInput> Rows)
    : IRequest<OperationResult<ImportProductsResponse>>;

public sealed class ImportProductRowInput
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public string Description { get; init; } = string.Empty;
}
