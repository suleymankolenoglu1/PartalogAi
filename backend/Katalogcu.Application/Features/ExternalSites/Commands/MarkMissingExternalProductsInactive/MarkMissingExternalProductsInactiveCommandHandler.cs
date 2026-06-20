using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.MarkMissingExternalProductsInactive;

public sealed class MarkMissingExternalProductsInactiveCommandHandler : IRequestHandler<MarkMissingExternalProductsInactiveCommand, OperationResult<int>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalSiteRepository _externalSiteRepository;
    private readonly IExternalProductUpsertService _externalProductUpsertService;

    public MarkMissingExternalProductsInactiveCommandHandler(
        ICurrentUserService currentUser,
        IExternalSiteRepository externalSiteRepository,
        IExternalProductUpsertService externalProductUpsertService)
    {
        _currentUser = currentUser;
        _externalSiteRepository = externalSiteRepository;
        _externalProductUpsertService = externalProductUpsertService;
    }

    public async Task<OperationResult<int>> Handle(MarkMissingExternalProductsInactiveCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<int>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var site = await _externalSiteRepository.GetSiteByIdAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        if (site is null)
        {
            return OperationResult<int>.Failure("not_found", "Site kaydı bulunamadı.");
        }

        var count = await _externalProductUpsertService.MarkMissingInactiveAsync(site.Id, request.SeenSourceUrls, cancellationToken);
        return OperationResult<int>.Success(count);
    }
}
