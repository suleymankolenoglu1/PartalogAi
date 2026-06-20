using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogExternalMatchReviewService
{
    void ApproveMatch(
        CatalogItemExternalMatch selectedMatch,
        IEnumerable<CatalogItemExternalMatch> allMatchesForCatalogItem,
        Guid reviewerUserId,
        DateTime reviewedAtUtc);

    void RejectMatch(
        CatalogItemExternalMatch selectedMatch,
        Guid reviewerUserId,
        DateTime reviewedAtUtc);

    CatalogItemExternalMatch CreateApprovedMatchFromProduct(
        CatalogItem catalogItem,
        ExternalSite externalSite,
        ExternalProduct externalProduct,
        Guid reviewerUserId,
        DateTime reviewedAtUtc,
        string? reviewNote);
}
