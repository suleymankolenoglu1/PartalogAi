using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class EmbedTarget : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Name { get; set; } = string.Empty;

    // catalog | catalog_page
    public string Type { get; set; } = "catalog";

    public Guid CatalogId { get; set; }
    public Catalog? Catalog { get; set; }

    public Guid? CatalogPageId { get; set; }
    public CatalogPage? CatalogPage { get; set; }

    public string EmbedKey { get; set; } = string.Empty;

    // catalog_only | host_cart | host_availability_cart
    public string CommerceMode { get; set; } = "catalog_only";

    // none | product_redirect | search_redirect | existing_cart_api | existing_cart_js | custom
    public string HostActionMode { get; set; } = "none";

    public string? ProductUrlTemplate { get; set; }

    public string? SearchUrlTemplate { get; set; }

    public string? ExistingCartUrl { get; set; }

    public string? ExistingCartMethod { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? AccessExpiresAt { get; set; }
}
