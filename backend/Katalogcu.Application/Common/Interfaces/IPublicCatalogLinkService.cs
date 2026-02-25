namespace Katalogcu.Application.Common.Interfaces;

public interface IPublicCatalogLinkService
{
    string CreateToken(Guid userId, int publicLinkVersion, IEnumerable<Guid>? catalogIds = null);
}
