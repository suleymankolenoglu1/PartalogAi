using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalLinkPublishingService
{
    Task<IReadOnlyList<PublishedExternalLinkDto>> GetPublishedLinksByCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<PublishedExternalLinkDto?> GetPublishedLinkByCatalogItemAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken);
    Task<ExternalLinkHealthRefreshResult> RefreshLinkHealthAsync(Guid matchId, CancellationToken cancellationToken);
    void MarkBroken(CatalogItemExternalMatch match, DateTime changedAtUtc);
    void RestoreApproved(CatalogItemExternalMatch match, DateTime changedAtUtc);
}
