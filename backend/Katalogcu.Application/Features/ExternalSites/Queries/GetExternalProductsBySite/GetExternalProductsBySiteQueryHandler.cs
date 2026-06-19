using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalProductsBySite;

public sealed class GetExternalProductsBySiteQueryHandler : IRequestHandler<GetExternalProductsBySiteQuery, OperationResult<ExternalProductsBySiteResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public GetExternalProductsBySiteQueryHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<ExternalProductsBySiteResponse>> Handle(GetExternalProductsBySiteQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ExternalProductsBySiteResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var site = await _externalSiteRepository.GetSiteByIdAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        if (site is null)
        {
            return OperationResult<ExternalProductsBySiteResponse>.Failure("not_found", "Site kaydı bulunamadı.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var skip = (page - 1) * pageSize;

        var (products, totalCount) = await _externalSiteRepository.GetProductsBySiteAsync(site.Id, _currentUser.UserId, skip, pageSize, cancellationToken);
        var latestMap = await _externalSiteRepository.GetLatestCrawlsBySiteIdsAsync([site.Id], cancellationToken);
        latestMap.TryGetValue(site.Id, out var crawl);

        return OperationResult<ExternalProductsBySiteResponse>.Success(new ExternalProductsBySiteResponse
        {
            SiteId = site.Id,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = products.Select(x => new ExternalProductListItemDto
            {
                Id = x.Id,
                Title = x.Title,
                SourceUrl = x.SourceUrl,
                CanonicalUrl = x.CanonicalUrl,
                Sku = x.Sku,
                PartCode = x.PartCode,
                Brand = x.Brand,
                LastSeenAtUtc = x.LastSeenAtUtc,
                IsActive = x.IsActive,
                OemCount = x.OemNumbers.Count
            }).ToList(),
            LatestCrawl = crawl is null
                ? null
                : new ExternalSiteCrawlSummaryDto
                {
                    Id = crawl.Id,
                    Status = crawl.Status,
                    ExecutionMode = crawl.ExecutionMode,
                    ProductCount = crawl.ProductCount,
                    SkuCoverage = crawl.SkuCoverage,
                    OemCoverage = crawl.OemCoverage,
                    ErrorSummary = crawl.ErrorSummary,
                    StartedAtUtc = crawl.StartedAtUtc,
                    CompletedAtUtc = crawl.CompletedAtUtc,
                    CreatedDate = crawl.CreatedDate
                }
        });
    }
}
