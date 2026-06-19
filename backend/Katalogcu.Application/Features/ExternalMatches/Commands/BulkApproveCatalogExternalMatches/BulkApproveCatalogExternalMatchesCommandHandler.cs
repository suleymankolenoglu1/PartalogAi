using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.BulkApproveCatalogExternalMatches;

public sealed class BulkApproveCatalogExternalMatchesCommandHandler : IRequestHandler<BulkApproveCatalogExternalMatchesCommand, OperationResult<BulkApproveCatalogExternalMatchesResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchReviewService _reviewService;

    public BulkApproveCatalogExternalMatchesCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchReviewService reviewService)
    {
        _currentUser = currentUser;
        _repository = repository;
        _reviewService = reviewService;
    }

    public async Task<OperationResult<BulkApproveCatalogExternalMatchesResponse>> Handle(BulkApproveCatalogExternalMatchesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<BulkApproveCatalogExternalMatchesResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var approvedCount = 0;
        var reviewedAtUtc = DateTime.UtcNow;

        foreach (var matchId in request.MatchIds.Distinct())
        {
            var match = await _repository.GetMatchByIdAsync(matchId, _currentUser.UserId, cancellationToken);
            if (match is null)
            {
                continue;
            }

            var relatedMatches = await _repository.GetMatchesByCatalogItemIdAsync(match.CatalogItemId, _currentUser.UserId, cancellationToken);
            _reviewService.ApproveMatch(match, relatedMatches, _currentUser.UserId, reviewedAtUtc);
            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
            {
                match.ReviewNote = request.ReviewNote.Trim();
            }

            approvedCount++;
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<BulkApproveCatalogExternalMatchesResponse>.Success(new BulkApproveCatalogExternalMatchesResponse
        {
            ApprovedCount = approvedCount
        });
    }
}
