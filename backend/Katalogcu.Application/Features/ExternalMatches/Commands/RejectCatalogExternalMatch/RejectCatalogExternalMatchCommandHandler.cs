using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.RejectCatalogExternalMatch;

public sealed class RejectCatalogExternalMatchCommandHandler : IRequestHandler<RejectCatalogExternalMatchCommand, OperationResult<RejectCatalogExternalMatchResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchReviewService _reviewService;

    public RejectCatalogExternalMatchCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchReviewService reviewService)
    {
        _currentUser = currentUser;
        _repository = repository;
        _reviewService = reviewService;
    }

    public async Task<OperationResult<RejectCatalogExternalMatchResponse>> Handle(RejectCatalogExternalMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<RejectCatalogExternalMatchResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var match = await _repository.GetMatchByIdAsync(request.MatchId, _currentUser.UserId, cancellationToken);
        if (match is null)
        {
            return OperationResult<RejectCatalogExternalMatchResponse>.Failure("not_found", "Eşleşme kaydı bulunamadı.");
        }

        _reviewService.RejectMatch(match, _currentUser.UserId, DateTime.UtcNow);
        match.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? match.ReviewNote : request.ReviewNote.Trim();

        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<RejectCatalogExternalMatchResponse>.Success(new RejectCatalogExternalMatchResponse
        {
            MatchId = match.Id,
            Status = match.Status
        });
    }
}
