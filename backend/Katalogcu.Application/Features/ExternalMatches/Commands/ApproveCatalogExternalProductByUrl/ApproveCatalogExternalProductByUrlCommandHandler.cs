using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalProductByUrl;

public sealed class ApproveCatalogExternalProductByUrlCommandHandler : IRequestHandler<ApproveCatalogExternalProductByUrlCommand, OperationResult<ApproveCatalogExternalProductByUrlResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchReviewService _reviewService;

    public ApproveCatalogExternalProductByUrlCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchReviewService reviewService)
    {
        _currentUser = currentUser;
        _repository = repository;
        _reviewService = reviewService;
    }

    public async Task<OperationResult<ApproveCatalogExternalProductByUrlResponse>> Handle(ApproveCatalogExternalProductByUrlCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ApproveCatalogExternalProductByUrlResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var catalogItem = await _repository.GetCatalogItemByIdWithCatalogAsync(request.CatalogItemId, _currentUser.UserId, cancellationToken);
        if (catalogItem is null)
        {
            return OperationResult<ApproveCatalogExternalProductByUrlResponse>.Failure("not_found", "Katalog item bulunamadı.");
        }

        var externalSite = await _repository.GetExternalSiteByIdAsync(request.ExternalSiteId, _currentUser.UserId, cancellationToken);
        if (externalSite is null)
        {
            return OperationResult<ApproveCatalogExternalProductByUrlResponse>.Failure("not_found", "Dış site kaydı bulunamadı.");
        }

        var normalizedUrl = request.ProductUrl.Trim().TrimEnd('/');
        var externalProduct = await _repository.GetExternalProductBySourceUrlAsync(externalSite.Id, normalizedUrl, _currentUser.UserId, cancellationToken);
        if (externalProduct is null)
        {
            externalProduct = new ExternalProduct
            {
                Id = Guid.NewGuid(),
                ExternalSiteId = externalSite.Id,
                SourceUrl = normalizedUrl,
                CanonicalUrl = normalizedUrl,
                Title = string.IsNullOrWhiteSpace(request.ProductTitle) ? null : request.ProductTitle.Trim(),
                RawPayloadJson = null,
                IsActive = true,
                LastSeenAtUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddExternalProductAsync(externalProduct, cancellationToken);
        }

        var relatedMatches = await _repository.GetMatchesByCatalogItemIdAsync(catalogItem.Id, _currentUser.UserId, cancellationToken);
        var reviewTime = DateTime.UtcNow;

        var manualMatch = _reviewService.CreateApprovedMatchFromProduct(
            catalogItem,
            externalSite,
            externalProduct,
            _currentUser.UserId,
            reviewTime,
            request.ReviewNote);

        foreach (var approved in relatedMatches.Where(x => string.Equals(x.Status, "approved", StringComparison.OrdinalIgnoreCase) && x.IsActive))
        {
            approved.IsActive = false;
            approved.UpdatedDate = reviewTime;
        }

        await _repository.AddMatchesAsync([manualMatch], cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<ApproveCatalogExternalProductByUrlResponse>.Success(new ApproveCatalogExternalProductByUrlResponse
        {
            MatchId = manualMatch.Id,
            CatalogItemId = manualMatch.CatalogItemId,
            ExternalProductId = externalProduct.Id,
            Status = manualMatch.Status
        });
    }
}
