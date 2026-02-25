namespace Katalogcu.Application.Features.Products.Commands.AdjustStock;

public sealed class AdjustStockResponse
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public int PreviousQuantity { get; init; }
    public int NewQuantity { get; init; }
    public int Delta { get; init; }
}
