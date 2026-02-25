namespace Katalogcu.Application.Features.Products.Commands.ImportStock;

public sealed class ImportStockResponse
{
    public int TotalRows { get; init; }
    public int Updated { get; init; }
    public int Created { get; init; }
    public int Skipped { get; init; }
    public string Mode { get; init; } = "update_only";
    public IReadOnlyList<ImportStockSkippedRow> SkippedRows { get; init; } = [];
}

public sealed record ImportStockSkippedRow(int RowNumber, string? Code, string Reason);
