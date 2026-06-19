namespace Katalogcu.Application.Common.Models;

public sealed class CatalogListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public Guid? FolderId { get; init; }
    public int PartCount { get; init; }
}

public sealed class PagedCatalogListResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<CatalogListItemDto> Items { get; init; } = [];
}
