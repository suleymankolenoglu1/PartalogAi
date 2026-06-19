using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalProductUpsertService
{
    Task<int> UpsertAsync(
        Guid externalSiteId,
        Guid crawlId,
        IReadOnlyList<NormalizedExternalProductRecord> products,
        CancellationToken cancellationToken);

    Task<int> MarkMissingInactiveAsync(
        Guid externalSiteId,
        IReadOnlyCollection<string> seenSourceUrls,
        CancellationToken cancellationToken);
}
