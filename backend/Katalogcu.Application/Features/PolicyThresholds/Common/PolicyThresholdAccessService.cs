using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public sealed class PolicyThresholdAccessService : IPolicyThresholdAccessService
{
    private readonly IPolicyThresholdRepository _repository;

    public PolicyThresholdAccessService(IPolicyThresholdRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResult<bool>> ValidateScopeAccessAsync(
        string scopeType,
        string scopeKey,
        PolicyThresholdActor actor,
        CancellationToken cancellationToken)
    {
        if (scopeType is PolicyThreshold.GlobalScope or PolicyThreshold.BrandScope)
        {
            return actor.IsPlatformAdmin
                ? OperationResult<bool>.Success(true)
                : OperationResult<bool>.Failure("forbidden", "Bu scope için yetki yok.");
        }

        if (!Guid.TryParse(scopeKey, out var catalogId))
        {
            return OperationResult<bool>.Failure("validation", "Catalog scope için ScopeKey geçerli bir catalog id olmalıdır.");
        }

        if (actor.IsPlatformAdmin)
        {
            return OperationResult<bool>.Success(true);
        }

        var ownsCatalog = await _repository.UserOwnsCatalogAsync(catalogId, actor.UserId, cancellationToken);
        return ownsCatalog
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure("forbidden", "Bu catalog için yetki yok.");
    }
}
