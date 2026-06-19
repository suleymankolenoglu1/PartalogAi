using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalProductNormalizer
{
    IReadOnlyList<NormalizedExternalProductRecord> Normalize(
        Guid externalSiteId,
        Guid crawlId,
        IReadOnlyList<CrawledProduct> products);
}
