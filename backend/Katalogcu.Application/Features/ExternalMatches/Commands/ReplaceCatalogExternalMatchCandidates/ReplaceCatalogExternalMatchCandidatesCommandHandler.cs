using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ReplaceCatalogExternalMatchCandidates;

public sealed class ReplaceCatalogExternalMatchCandidatesCommandHandler : IRequestHandler<ReplaceCatalogExternalMatchCandidatesCommand, OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchService _matchService;

    public ReplaceCatalogExternalMatchCandidatesCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchService matchService)
    {
        _currentUser = currentUser;
        _repository = repository;
        _matchService = matchService;
    }

    public async Task<OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>> Handle(ReplaceCatalogExternalMatchCandidatesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var catalog = await _repository.GetOwnedCatalogWithPagesAsync(request.CatalogId, _currentUser.UserId, cancellationToken);
        if (catalog is null)
        {
            return OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>.Failure("not_found", "Katalog bulunamadı.");
        }

        var existingMatches = await _repository.GetMatchesByCatalogIdAsync(catalog.Id, _currentUser.UserId, cancellationToken);
        _matchService.ReplaceAiCandidates(catalog.Id, existingMatches, [], out var matchesToRemove, out _);

        if (matchesToRemove.Count > 0)
        {
            _repository.RemoveMatches(matchesToRemove);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        var matchesToAddCount = 0;
        var catalogItemBatchSize = _matchService.CatalogItemBatchSize;
        var batchSize = _matchService.ExternalProductBatchSize;
        for (var catalogItemSkip = 0; ; catalogItemSkip += catalogItemBatchSize)
        {
            var catalogItemsBatch = await _repository.GetCatalogItemsByCatalogIdAsync(
                catalog.Id,
                _currentUser.UserId,
                catalogItemSkip,
                catalogItemBatchSize,
                cancellationToken);

            if (catalogItemsBatch.Count == 0)
            {
                break;
            }

            for (var externalProductSkip = 0; ; externalProductSkip += batchSize)
            {
                var externalProductsBatch = await _repository.GetActiveExternalProductsBySiteIdAsync(
                    request.ExternalSiteId,
                    _currentUser.UserId,
                    externalProductSkip,
                    batchSize,
                    cancellationToken);

                if (externalProductsBatch.Count == 0)
                {
                    break;
                }

                var batchCandidates = _matchService.BuildCandidates(catalog, catalogItemsBatch, externalProductsBatch, request.ExternalSiteId);
                if (batchCandidates.Count > 0)
                {
                    await _repository.AddMatchesAsync(batchCandidates, cancellationToken);
                    await _repository.SaveChangesAsync(cancellationToken);
                    matchesToAddCount += batchCandidates.Count;
                }

                if (externalProductsBatch.Count < batchSize)
                {
                    break;
                }
            }

            if (catalogItemsBatch.Count < catalogItemBatchSize)
            {
                break;
            }
        }

        return OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>.Success(new ReplaceCatalogExternalMatchCandidatesResponse
        {
            CatalogId = catalog.Id,
            ExternalSiteId = request.ExternalSiteId,
            AddedCount = matchesToAddCount,
            RemovedCount = matchesToRemove.Count
        });
    }
}
