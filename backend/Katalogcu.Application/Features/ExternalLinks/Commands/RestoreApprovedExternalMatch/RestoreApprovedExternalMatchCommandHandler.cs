using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RestoreApprovedExternalMatch;

public sealed class RestoreApprovedExternalMatchCommandHandler
    : IRequestHandler<RestoreApprovedExternalMatchCommand, OperationResult<RestoreApprovedExternalMatchResponse>>
{
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly IExternalLinkPublishingService _service;

    public RestoreApprovedExternalMatchCommandHandler(
        ICatalogExternalMatchRepository repository,
        IExternalLinkPublishingService service)
    {
        _repository = repository;
        _service = service;
    }

    public async Task<OperationResult<RestoreApprovedExternalMatchResponse>> Handle(RestoreApprovedExternalMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _repository.GetMatchByIdForLinkRefreshAsync(request.MatchId, cancellationToken);
        if (match is null)
        {
            return OperationResult<RestoreApprovedExternalMatchResponse>.Failure("not_found", "Eşleşme kaydı bulunamadı.");
        }

        _service.RestoreApproved(match, DateTime.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<RestoreApprovedExternalMatchResponse>.Success(new RestoreApprovedExternalMatchResponse
        {
            MatchId = match.Id,
            Status = match.Status,
            IsLinkHealthy = match.IsLinkHealthy
        });
    }
}
