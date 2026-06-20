using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogApprovedExternalMatches;

public sealed class GetCatalogApprovedExternalMatchesQueryHandler
    : IRequestHandler<GetCatalogApprovedExternalMatchesQuery, OperationResult<GetCatalogApprovedExternalMatchesResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;

    public GetCatalogApprovedExternalMatchesQueryHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<OperationResult<GetCatalogApprovedExternalMatchesResponse>> Handle(GetCatalogApprovedExternalMatchesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<GetCatalogApprovedExternalMatchesResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var items = await _repository.GetApprovedMatchesByCatalogIdAsync(request.CatalogId, _currentUser.UserId, cancellationToken);
        return OperationResult<GetCatalogApprovedExternalMatchesResponse>.Success(new GetCatalogApprovedExternalMatchesResponse
        {
            Items = items.Select(x => new GetCatalogApprovedExternalMatchesItemDto
            {
                MatchId = x.Id,
                CatalogItemId = x.CatalogItemId,
                ExternalSiteId = x.ExternalSiteId,
                ExternalProductId = x.ExternalProductId,
                ExternalProductUrl = x.ExternalProductUrl,
                ExternalProductTitle = x.ExternalProductTitle,
                ConfidenceScore = x.ConfidenceScore,
                Status = x.Status,
                IsLinkHealthy = x.IsLinkHealthy,
                LastLinkCheckAtUtc = x.LastLinkCheckAtUtc,
                LastLinkStatusCode = x.LastLinkStatusCode,
                ReviewedByUserId = x.ReviewedByUserId,
                ReviewedAtUtc = x.ReviewedAtUtc
            }).ToList()
        });
    }
}
