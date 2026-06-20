using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSites;

public sealed class GetExternalSitesQueryHandler : IRequestHandler<GetExternalSitesQuery, OperationResult<IReadOnlyList<ExternalSiteDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public GetExternalSitesQueryHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<IReadOnlyList<ExternalSiteDto>>> Handle(GetExternalSitesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<ExternalSiteDto>>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var sites = await _externalSiteRepository.GetSitesByUserAsync(_currentUser.UserId, cancellationToken);
        var latestCrawls = await _externalSiteRepository.GetLatestCrawlsBySiteIdsAsync(sites.Select(x => x.Id).ToArray(), cancellationToken);

        var result = sites.Select(site => new ExternalSiteDto
        {
            Id = site.Id,
            Name = site.Name,
            BaseUrl = site.BaseUrl,
            Status = site.Status,
            PreferredCrawlMode = site.PreferredCrawlMode,
            LastCrawlAtUtc = site.LastCrawlAtUtc,
            LastSuccessfulCrawlAtUtc = site.LastSuccessfulCrawlAtUtc,
            CreatedDate = site.CreatedDate,
            LatestCrawl = latestCrawls.TryGetValue(site.Id, out var crawl)
                ? new ExternalSiteCrawlSummaryDto
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
                : null
        }).ToList();

        return OperationResult<IReadOnlyList<ExternalSiteDto>>.Success(result);
    }
}
