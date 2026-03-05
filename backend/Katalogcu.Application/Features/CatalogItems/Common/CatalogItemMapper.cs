using Katalogcu.Application.Features.Catalogs.Common;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.CatalogItems.Common;

internal static class CatalogItemMapper
{
    public static CatalogPageItemDto ToDto(CatalogItem item)
    {
        return new CatalogPageItemDto
        {
            CatalogItemId = item.Id,
            RefNo = item.RefNumber,
            PartCode = item.PartCode,
            PartName = item.PartName,
            Description = item.Description,
            IsStocked = false,
            ProductId = null,
            Price = null,
            LocalName = null
        };
    }
}
