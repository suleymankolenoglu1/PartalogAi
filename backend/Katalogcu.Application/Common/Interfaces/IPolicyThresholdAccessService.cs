using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPolicyThresholdAccessService
{
    Task<OperationResult<bool>> ValidateScopeAccessAsync(
        string scopeType,
        string scopeKey,
        PolicyThresholdActor actor,
        CancellationToken cancellationToken);
}
