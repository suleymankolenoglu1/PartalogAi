using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalMatch;

public sealed class ApproveCatalogExternalMatchCommandHandler : IRequestHandler<ApproveCatalogExternalMatchCommand, OperationResult<ApproveCatalogExternalMatchResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchReviewService _reviewService;

    public ApproveCatalogExternalMatchCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchReviewService reviewService)
    {
        _currentUser = currentUser;
        _repository = repository;
        _reviewService = reviewService;
    }

    public async Task<OperationResult<ApproveCatalogExternalMatchResponse>> Handle(ApproveCatalogExternalMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ApproveCatalogExternalMatchResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var match = await _repository.GetMatchByIdAsync(request.MatchId, _currentUser.UserId, cancellationToken);
        if (match is null)
        {
            return OperationResult<ApproveCatalogExternalMatchResponse>.Failure("not_found", "Eşleşme kaydı bulunamadı.");
        }

        var relatedMatches = await _repository.GetMatchesByCatalogItemIdAsync(match.CatalogItemId, _currentUser.UserId, cancellationToken);
        var reviewedAtUtc = DateTime.UtcNow;
        _reviewService.ApproveMatch(match, relatedMatches, _currentUser.UserId, reviewedAtUtc);
        match.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? match.ReviewNote : request.ReviewNote.Trim();

        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<ApproveCatalogExternalMatchResponse>.Success(new ApproveCatalogExternalMatchResponse
        {
            MatchId = match.Id,
            CatalogItemId = match.CatalogItemId,
            Status = match.Status
        });
    }
}
