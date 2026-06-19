namespace Katalogcu.Application.Features.Products.Queries.Common;

public sealed class PagedOwnedProductsResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ProductListItemDto> Items { get; init; } = [];
}
