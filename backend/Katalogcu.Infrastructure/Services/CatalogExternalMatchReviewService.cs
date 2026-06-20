using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Infrastructure.Services;

public sealed class CatalogExternalMatchReviewService : ICatalogExternalMatchReviewService
{
    public void ApproveMatch(
        CatalogItemExternalMatch selectedMatch,
        IEnumerable<CatalogItemExternalMatch> allMatchesForCatalogItem,
        Guid reviewerUserId,
        DateTime reviewedAtUtc)
    {
        foreach (var match in allMatchesForCatalogItem.Where(x => x.CatalogItemId == selectedMatch.CatalogItemId))
        {
            if (match.Id == selectedMatch.Id)
            {
                match.Status = "approved";
                match.IsActive = true;
                match.MatchedBy = "human";
                match.ReviewedByUserId = reviewerUserId;
                match.ReviewedAtUtc = reviewedAtUtc;
                match.MatchedAtUtc ??= reviewedAtUtc;
                match.UpdatedDate = reviewedAtUtc;
                match.IsLinkHealthy ??= true;
                continue;
            }

            if (string.Equals(match.Status, "approved", StringComparison.OrdinalIgnoreCase) && match.IsActive)
            {
                match.IsActive = false;
                match.UpdatedDate = reviewedAtUtc;
            }
        }
    }

    public void RejectMatch(
        CatalogItemExternalMatch selectedMatch,
        Guid reviewerUserId,
        DateTime reviewedAtUtc)
    {
        selectedMatch.Status = "rejected";
        selectedMatch.IsActive = false;
        selectedMatch.MatchedBy = "human";
        selectedMatch.ReviewedByUserId = reviewerUserId;
        selectedMatch.ReviewedAtUtc = reviewedAtUtc;
        selectedMatch.UpdatedDate = reviewedAtUtc;
    }

    public CatalogItemExternalMatch CreateApprovedMatchFromProduct(
        CatalogItem catalogItem,
        ExternalSite externalSite,
        ExternalProduct externalProduct,
        Guid reviewerUserId,
        DateTime reviewedAtUtc,
        string? reviewNote)
    {
        return new CatalogItemExternalMatch
        {
            Id = Guid.NewGuid(),
            CatalogId = catalogItem.CatalogId,
            CatalogPageId = null,
            CatalogItemId = catalogItem.Id,
            ExternalSiteId = externalSite.Id,
            ExternalProductId = externalProduct.Id,
            ExternalProductUrl = externalProduct.CanonicalUrl ?? externalProduct.SourceUrl,
            ExternalProductTitle = externalProduct.Title,
            ConfidenceScore = 1.0m,
            Status = "approved",
            MatchedBy = "human",
            IsActive = true,
            MatchedAtUtc = reviewedAtUtc,
            ReviewedByUserId = reviewerUserId,
            ReviewedAtUtc = reviewedAtUtc,
            ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim(),
            MatchReasonsJson = "[\"manual_url_approval\"]",
            IsLinkHealthy = true,
            LastLinkCheckAtUtc = reviewedAtUtc,
            CreatedDate = reviewedAtUtc
        };
    }
}
