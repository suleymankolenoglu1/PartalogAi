using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicToken;

public sealed class GetPublicTokenQueryHandler : IRequestHandler<GetPublicTokenQuery, OperationResult<string>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IPublicCatalogLinkService _publicCatalogLinkService;

    public GetPublicTokenQueryHandler(
        IUserRepository userRepository,
        ICatalogRepository catalogRepository,
        IPublicCatalogLinkService publicCatalogLinkService)
    {
        _userRepository = userRepository;
        _catalogRepository = catalogRepository;
        _publicCatalogLinkService = publicCatalogLinkService;
    }

    public async Task<OperationResult<string>> Handle(GetPublicTokenQuery request, CancellationToken cancellationToken)
    {
        var userState = await _userRepository.GetPublicLinkStateAsync(request.UserId, cancellationToken);
        if (userState == null)
        {
            return OperationResult<string>.Failure("unauthorized", "Unauthorized");
        }

        if (!userState.PublicLinkEnabled)
        {
            return OperationResult<string>.Failure("validation", "Public link devre dışı. Yeniden açmak için linki yenileyin.");
        }

        IReadOnlyList<Guid> allowedIds = [];
        if (request.RequestedCatalogIds.Count > 0)
        {
            allowedIds = await _catalogRepository.GetPublishedCatalogIdsByUserAsync(
                request.UserId,
                request.RequestedCatalogIds,
                cancellationToken);

            if (allowedIds.Count == 0)
            {
                return OperationResult<string>.Failure("validation", "Seçilen kataloglar yayınlanmamış veya size ait değil.");
            }
        }

        var token = _publicCatalogLinkService.CreateToken(
            request.UserId,
            userState.PublicLinkVersion,
            allowedIds.Count > 0 ? allowedIds : null);

        return OperationResult<string>.Success(token);
    }
}
