using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RefreshApprovedExternalLinkHealth;

public sealed class RefreshApprovedExternalLinkHealthCommandHandler
    : IRequestHandler<RefreshApprovedExternalLinkHealthCommand, OperationResult<RefreshApprovedExternalLinkHealthResponse>>
{
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly IExternalLinkPublishingService _service;

    public RefreshApprovedExternalLinkHealthCommandHandler(
        ICatalogExternalMatchRepository repository,
        IExternalLinkPublishingService service)
    {
        _repository = repository;
        _service = service;
    }

    public async Task<OperationResult<RefreshApprovedExternalLinkHealthResponse>> Handle(RefreshApprovedExternalLinkHealthCommand request, CancellationToken cancellationToken)
    {
        var match = await _repository.GetMatchByIdForLinkRefreshAsync(request.MatchId, cancellationToken);
        if (match is null)
        {
            return OperationResult<RefreshApprovedExternalLinkHealthResponse>.Failure("not_found", "Eşleşme kaydı bulunamadı.");
        }

        if (match.ExternalProductId is null)
        {
            return OperationResult<RefreshApprovedExternalLinkHealthResponse>.Failure("invalid_match", "Eşleşme kaydında dış ürün bilgisi yok.");
        }

        var result = await _service.RefreshLinkHealthAsync(request.MatchId, cancellationToken);
        return OperationResult<RefreshApprovedExternalLinkHealthResponse>.Success(new RefreshApprovedExternalLinkHealthResponse
        {
            MatchId = result.MatchId,
            ExternalProductId = result.ExternalProductId,
            Status = result.Status,
            IsReachable = result.IsReachable,
            StatusCode = result.StatusCode,
            CheckedAtUtc = result.CheckedAtUtc,
            FinalUrl = result.FinalUrl,
            ErrorSummary = result.ErrorSummary
        });
    }
}
