using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyThresholds;

public sealed class GetPolicyThresholdsQueryHandler
    : IRequestHandler<GetPolicyThresholdsQuery, OperationResult<IReadOnlyList<PolicyThresholdDto>>>
{
    private readonly IPolicyThresholdRepository _repository;

    public GetPolicyThresholdsQueryHandler(IPolicyThresholdRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResult<IReadOnlyList<PolicyThresholdDto>>> Handle(
        GetPolicyThresholdsQuery request,
        CancellationToken cancellationToken)
    {
        var scopeType = string.IsNullOrWhiteSpace(request.ScopeType)
            ? null
            : PolicyThresholdRules.NormalizeScopeType(request.ScopeType);

        if (!string.IsNullOrWhiteSpace(request.ScopeType) && scopeType is null)
        {
            return OperationResult<IReadOnlyList<PolicyThresholdDto>>.Failure("validation", "Geçersiz scope tipi.");
        }

        var thresholds = await _repository.GetPolicyThresholdsAsync(
            request.IncludeInactive,
            scopeType,
            request.Actor.UserId,
            request.Actor.IsPlatformAdmin,
            cancellationToken);

        return OperationResult<IReadOnlyList<PolicyThresholdDto>>.Success(
            thresholds
                .OrderBy(x => x.ScopeType)
                .ThenBy(x => x.ScopeKey)
                .ThenByDescending(x => x.Version)
                .Select(PolicyThresholdMapper.ToDto)
                .ToList());
    }
}
