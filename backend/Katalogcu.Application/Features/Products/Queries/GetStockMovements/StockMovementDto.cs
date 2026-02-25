namespace Katalogcu.Application.Features.Products.Queries.GetStockMovements;

public sealed class StockMovementDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int PreviousQuantity { get; init; }
    public int DeltaQuantity { get; init; }
    public int NewQuantity { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? Source { get; init; }
    public string? ActorName { get; init; }
    public string? ReferenceId { get; init; }
    public DateTime CreatedDate { get; init; }
}
