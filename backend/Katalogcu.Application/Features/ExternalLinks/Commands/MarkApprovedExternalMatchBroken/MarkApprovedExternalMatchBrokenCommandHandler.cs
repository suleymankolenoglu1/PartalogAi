using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.MarkApprovedExternalMatchBroken;

public sealed class MarkApprovedExternalMatchBrokenCommandHandler
    : IRequestHandler<MarkApprovedExternalMatchBrokenCommand, OperationResult<MarkApprovedExternalMatchBrokenResponse>>
{
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly IExternalLinkPublishingService _service;

    public MarkApprovedExternalMatchBrokenCommandHandler(
        ICatalogExternalMatchRepository repository,
        IExternalLinkPublishingService service)
    {
        _repository = repository;
        _service = service;
    }

    public async Task<OperationResult<MarkApprovedExternalMatchBrokenResponse>> Handle(MarkApprovedExternalMatchBrokenCommand request, CancellationToken cancellationToken)
    {
        var match = await _repository.GetMatchByIdForLinkRefreshAsync(request.MatchId, cancellationToken);
        if (match is null)
        {
            return OperationResult<MarkApprovedExternalMatchBrokenResponse>.Failure("not_found", "Eşleşme kaydı bulunamadı.");
        }

        _service.MarkBroken(match, DateTime.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<MarkApprovedExternalMatchBrokenResponse>.Success(new MarkApprovedExternalMatchBrokenResponse
        {
            MatchId = match.Id,
            Status = match.Status,
            IsLinkHealthy = match.IsLinkHealthy
        });
    }
}
