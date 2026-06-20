using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinkByCatalogItem;

public sealed class GetPublishedExternalLinkByCatalogItemQueryHandler
    : IRequestHandler<GetPublishedExternalLinkByCatalogItemQuery, OperationResult<GetPublishedExternalLinkByCatalogItemResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalLinkPublishingService _service;

    public GetPublishedExternalLinkByCatalogItemQueryHandler(
        ICurrentUserService currentUser,
        IExternalLinkPublishingService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public async Task<OperationResult<GetPublishedExternalLinkByCatalogItemResponse>> Handle(GetPublishedExternalLinkByCatalogItemQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<GetPublishedExternalLinkByCatalogItemResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var link = await _service.GetPublishedLinkByCatalogItemAsync(request.CatalogItemId, _currentUser.UserId, cancellationToken);
        return OperationResult<GetPublishedExternalLinkByCatalogItemResponse>.Success(new GetPublishedExternalLinkByCatalogItemResponse
        {
            Link = link
        });
    }
}
