using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.StartExternalSiteCrawl;

public sealed class StartExternalSiteCrawlCommandHandler : IRequestHandler<StartExternalSiteCrawlCommand, OperationResult<StartExternalSiteCrawlResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;
    private readonly IExternalSiteCrawlBackgroundProcessor _backgroundProcessor;

    public StartExternalSiteCrawlCommandHandler(
        ICurrentUserService currentUser,
        IExternalSiteRepository externalSiteRepository,
        IExternalSiteCrawlBackgroundProcessor backgroundProcessor)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
        _backgroundProcessor = backgroundProcessor;
    }

    public async Task<OperationResult<StartExternalSiteCrawlResponse>> Handle(StartExternalSiteCrawlCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<StartExternalSiteCrawlResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var site = await _externalSiteRepository.GetSiteByIdAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        if (site is null)
        {
            return OperationResult<StartExternalSiteCrawlResponse>.Failure("not_found", "Site kaydı bulunamadı.");
        }

        if (!string.Equals(site.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<StartExternalSiteCrawlResponse>.Failure("validation", "Sadece aktif sitelerde tarama başlatılabilir.");
        }

        var crawlId = await _backgroundProcessor.EnqueueAsync(site.Id, cancellationToken);
        return OperationResult<StartExternalSiteCrawlResponse>.Success(new StartExternalSiteCrawlResponse
        {
            SiteId = site.Id,
            CrawlId = crawlId,
            Status = "queued"
        });
    }
}
