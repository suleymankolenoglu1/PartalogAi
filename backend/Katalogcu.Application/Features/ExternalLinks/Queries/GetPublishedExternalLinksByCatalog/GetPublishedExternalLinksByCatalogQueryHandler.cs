using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinksByCatalog;

public sealed class GetPublishedExternalLinksByCatalogQueryHandler
    : IRequestHandler<GetPublishedExternalLinksByCatalogQuery, OperationResult<GetPublishedExternalLinksByCatalogResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalLinkPublishingService _service;

    public GetPublishedExternalLinksByCatalogQueryHandler(
        ICurrentUserService currentUser,
        IExternalLinkPublishingService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public async Task<OperationResult<GetPublishedExternalLinksByCatalogResponse>> Handle(GetPublishedExternalLinksByCatalogQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<GetPublishedExternalLinksByCatalogResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var links = await _service.GetPublishedLinksByCatalogAsync(request.CatalogId, _currentUser.UserId, cancellationToken);
        return OperationResult<GetPublishedExternalLinksByCatalogResponse>.Success(new GetPublishedExternalLinksByCatalogResponse
        {
            Links = links
        });
    }
}
