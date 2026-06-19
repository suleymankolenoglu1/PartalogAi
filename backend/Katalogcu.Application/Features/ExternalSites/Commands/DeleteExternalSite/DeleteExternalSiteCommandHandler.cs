using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.DeleteExternalSite;

public sealed class DeleteExternalSiteCommandHandler : IRequestHandler<DeleteExternalSiteCommand, OperationResult<bool>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;

    public DeleteExternalSiteCommandHandler(ICurrentUserService currentUser, IExternalSiteRepository externalSiteRepository)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
    }

    public async Task<OperationResult<bool>> Handle(DeleteExternalSiteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<bool>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var site = await _externalSiteRepository.GetSiteByIdAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        if (site is null)
        {
            return OperationResult<bool>.Failure("not_found", "Site kaydı bulunamadı.");
        }

        _externalSiteRepository.RemoveSite(site);
        await _externalSiteRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}
