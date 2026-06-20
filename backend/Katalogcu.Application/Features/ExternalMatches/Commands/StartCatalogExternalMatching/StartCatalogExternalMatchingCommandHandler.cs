using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.StartCatalogExternalMatching;

public sealed class StartCatalogExternalMatchingCommandHandler
    : IRequestHandler<StartCatalogExternalMatchingCommand, OperationResult<StartCatalogExternalMatchingResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ICatalogExternalMatchService _service;

    public StartCatalogExternalMatchingCommandHandler(
        ICurrentUserService currentUser,
        ICatalogExternalMatchRepository repository,
        ICatalogExternalMatchService service)
    {
        _currentUser = currentUser;
        _repository = repository;
        _service = service;
    }

    public async Task<OperationResult<StartCatalogExternalMatchingResponse>> Handle(
        StartCatalogExternalMatchingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<StartCatalogExternalMatchingResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var catalog = await _repository.GetOwnedCatalogWithPagesAsync(request.CatalogId, _currentUser.UserId, cancellationToken);
        if (catalog is null)
        {
            return OperationResult<StartCatalogExternalMatchingResponse>.Failure("not_found", "Katalog bulunamadı.");
        }

        var externalSite = await _repository.GetExternalSiteByIdAsync(request.ExternalSiteId, _currentUser.UserId, cancellationToken);
        if (externalSite is null)
        {
            return OperationResult<StartCatalogExternalMatchingResponse>.Failure("not_found", "E-ticaret sitesi bulunamadı.");
        }

        var existingMatches = await _repository.GetMatchesByCatalogIdAsync(request.CatalogId, _currentUser.UserId, cancellationToken);
        _service.ReplaceAiCandidates(request.CatalogId, existingMatches, [], out var matchesToRemove, out _);

        if (matchesToRemove.Count > 0)
        {
            _repository.RemoveMatches(matchesToRemove);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        var matchesToAddCount = 0;
        var catalogItemBatchSize = _service.CatalogItemBatchSize;
        var batchSize = _service.ExternalProductBatchSize;
        for (var catalogItemSkip = 0; ; catalogItemSkip += catalogItemBatchSize)
        {
            var catalogItemsBatch = await _repository.GetCatalogItemsByCatalogIdAsync(
                request.CatalogId,
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

                var batchCandidates = _service.BuildCandidates(catalog, catalogItemsBatch, externalProductsBatch, request.ExternalSiteId);
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

        return OperationResult<StartCatalogExternalMatchingResponse>.Success(new StartCatalogExternalMatchingResponse
        {
            CatalogId = request.CatalogId,
            ExternalSiteId = request.ExternalSiteId,
            CandidateCount = matchesToAddCount
        });
    }
}
