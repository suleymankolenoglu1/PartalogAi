using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class PublicCatalogLinkService : IPublicCatalogLinkService
{
    private readonly IPublicLinkService _publicLinkService;

    public PublicCatalogLinkService(IPublicLinkService publicLinkService)
    {
        _publicLinkService = publicLinkService;
    }

    public string CreateToken(Guid userId, int publicLinkVersion, IEnumerable<Guid>? catalogIds = null)
    {
        return _publicLinkService.CreateToken(userId, publicLinkVersion, catalogIds);
    }
}
