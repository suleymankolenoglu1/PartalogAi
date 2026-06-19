using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;
using System.Text.Encodings.Web;

namespace Katalogcu.Application.Features.ExternalSites.Commands.UpdateExternalSite;

public sealed class UpdateExternalSiteCommandHandler : IRequestHandler<UpdateExternalSiteCommand, OperationResult<ExternalSiteDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public UpdateExternalSiteCommandHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<ExternalSiteDto>> Handle(UpdateExternalSiteCommand request, CancellationToken cancellationToken)
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

        var normalizedUrl = NormalizeBaseUrl(request.BaseUrl);
        var exists = await _externalSiteRepository.BaseUrlExistsAsync(_currentUser.UserId, normalizedUrl, request.SiteId, cancellationToken);
        if (exists)
        {
            return OperationResult<ExternalSiteDto>.Failure("duplicate", "Bu site adresi zaten kayıtlı.");
        }

        site.Name = SanitizeText(request.Name);
        site.BaseUrl = normalizedUrl;
        site.PreferredCrawlMode = request.PreferredCrawlMode.Trim();
        site.Status = request.Status.Trim();
        site.UpdatedDate = DateTime.UtcNow;

        await _externalSiteRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<ExternalSiteDto>.Success(new ExternalSiteDto
        {
            Id = site.Id,
            Name = site.Name,
            BaseUrl = site.BaseUrl,
            Status = site.Status,
            PreferredCrawlMode = site.PreferredCrawlMode,
            LastCrawlAtUtc = site.LastCrawlAtUtc,
            LastSuccessfulCrawlAtUtc = site.LastSuccessfulCrawlAtUtc,
            CreatedDate = site.CreatedDate
        });
    }

    private static string NormalizeBaseUrl(string url)
    {
        var trimmed = url.Trim();
        var builder = new UriBuilder(trimmed)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string SanitizeText(string value)
    {
        return HtmlEncoder.Default.Encode(value.Trim());
    }
}
