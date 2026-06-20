using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using Katalogcu.Domain.Entities;
using MediatR;
using System.Text.Encodings.Web;

namespace Katalogcu.Application.Features.ExternalSites.Commands.CreateExternalSite;

public sealed class CreateExternalSiteCommandHandler : IRequestHandler<CreateExternalSiteCommand, OperationResult<ExternalSiteDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public CreateExternalSiteCommandHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<ExternalSiteDto>> Handle(CreateExternalSiteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ExternalSiteDto>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var normalizedUrl = NormalizeBaseUrl(request.BaseUrl);
        var exists = await _externalSiteRepository.BaseUrlExistsAsync(_currentUser.UserId, normalizedUrl, null, cancellationToken);
        if (exists)
        {
            return OperationResult<ExternalSiteDto>.Failure("duplicate", "Bu site adresi zaten kayıtlı.");
        }

        var site = new ExternalSite
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Name = SanitizeText(request.Name),
            BaseUrl = normalizedUrl,
            PreferredCrawlMode = request.PreferredCrawlMode.Trim(),
            Status = "active",
            CreatedDate = DateTime.UtcNow
        };

        await _externalSiteRepository.AddSiteAsync(site, cancellationToken);
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
