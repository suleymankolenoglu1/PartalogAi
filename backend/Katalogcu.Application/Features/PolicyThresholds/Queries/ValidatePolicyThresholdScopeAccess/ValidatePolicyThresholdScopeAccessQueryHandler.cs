using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.ValidatePolicyThresholdScopeAccess;

public sealed class ValidatePolicyThresholdScopeAccessQueryHandler
    : IRequestHandler<ValidatePolicyThresholdScopeAccessQuery, OperationResult<bool>>
{
    private readonly IPolicyThresholdAccessService _accessService;

    public ValidatePolicyThresholdScopeAccessQueryHandler(IPolicyThresholdAccessService accessService)
    {
        _accessService = accessService;
    }

    public async Task<OperationResult<bool>> Handle(
        ValidatePolicyThresholdScopeAccessQuery request,
        CancellationToken cancellationToken)
    {
        return await _accessService.ValidateScopeAccessAsync(
            request.ScopeType,
            request.ScopeKey,
            request.Actor,
            cancellationToken);
    }
}
