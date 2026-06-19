using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogExternalMatchService
{
    int CatalogItemBatchSize { get; }
    int ExternalProductBatchSize { get; }

    IReadOnlyList<CatalogItemExternalMatch> BuildCandidates(
        Catalog catalog,
        IReadOnlyList<CatalogItem> catalogItems,
        IReadOnlyList<ExternalProduct> externalProducts,
        Guid externalSiteId);

    void ReplaceAiCandidates(
        Guid catalogId,
        IEnumerable<CatalogItemExternalMatch> existingMatches,
        IEnumerable<CatalogItemExternalMatch> newMatches,
        out IReadOnlyList<CatalogItemExternalMatch> matchesToRemove,
        out IReadOnlyList<CatalogItemExternalMatch> matchesToAdd);
}
