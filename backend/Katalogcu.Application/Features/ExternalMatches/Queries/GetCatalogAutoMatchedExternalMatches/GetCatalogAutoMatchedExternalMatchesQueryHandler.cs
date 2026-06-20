using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogAutoMatchedExternalMatches;

public sealed class GetCatalogAutoMatchedExternalMatchesQueryHandler
    : IRequestHandler<GetCatalogAutoMatchedExternalMatchesQuery, OperationResult<GetCatalogAutoMatchedExternalMatchesResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;

    public GetCatalogAutoMatchedExternalMatchesQueryHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<OperationResult<GetCatalogAutoMatchedExternalMatchesResponse>> Handle(
        GetCatalogAutoMatchedExternalMatchesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<GetCatalogAutoMatchedExternalMatchesResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);
        var (items, totalCount) = await _repository.GetAutoMatchedMatchesByCatalogIdAsync(
            request.CatalogId,
            _currentUser.UserId,
            page,
            pageSize,
            cancellationToken);

        return OperationResult<GetCatalogAutoMatchedExternalMatchesResponse>.Success(new GetCatalogAutoMatchedExternalMatchesResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items.Select(x => new GetCatalogAutoMatchedExternalMatchesItemDto
            {
                MatchId = x.Id,
                CatalogItemId = x.CatalogItemId,
                ExternalSiteId = x.ExternalSiteId,
                ExternalProductId = x.ExternalProductId,
                ExternalProductUrl = x.ExternalProductUrl,
                ExternalProductTitle = x.ExternalProductTitle,
                ConfidenceScore = x.ConfidenceScore,
                Status = x.Status,
                MatchedBy = x.MatchedBy,
                MatchedAtUtc = x.MatchedAtUtc,
                MatchReasonsJson = x.MatchReasonsJson
            }).ToList()
        });
    }
}
