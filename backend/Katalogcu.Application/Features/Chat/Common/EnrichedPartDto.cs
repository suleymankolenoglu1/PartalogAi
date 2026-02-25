namespace Katalogcu.Application.Features.Chat.Common;

public sealed class EnrichedPartDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Model { get; init; }
    public Guid CatalogId { get; init; }
    public string? PageNumber { get; init; }
    public string StockStatus { get; init; } = "Bilinmiyor";
    public decimal? Price { get; init; }
    public string? ImageUrl { get; init; }
}
