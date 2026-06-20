using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSiteById;

public sealed class GetExternalSiteByIdQueryHandler : IRequestHandler<GetExternalSiteByIdQuery, OperationResult<ExternalSiteDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public GetExternalSiteByIdQueryHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<ExternalSiteDto>> Handle(GetExternalSiteByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ExternalSiteDto>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var site = await _externalSiteRepository.GetSiteByIdAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        if (site is null)
        {
            return OperationResult<ExternalSiteDto>.Failure("not_found", "Site kaydı bulunamadı.");
        }

        var latestMap = await _externalSiteRepository.GetLatestCrawlsBySiteIdsAsync([site.Id], cancellationToken);
        latestMap.TryGetValue(site.Id, out var crawl);

        return OperationResult<ExternalSiteDto>.Success(new ExternalSiteDto
        {
            Id = site.Id,
            Name = site.Name,
            BaseUrl = site.BaseUrl,
            Status = site.Status,
            PreferredCrawlMode = site.PreferredCrawlMode,
            LastCrawlAtUtc = site.LastCrawlAtUtc,
            LastSuccessfulCrawlAtUtc = site.LastSuccessfulCrawlAtUtc,
            CreatedDate = site.CreatedDate,
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
