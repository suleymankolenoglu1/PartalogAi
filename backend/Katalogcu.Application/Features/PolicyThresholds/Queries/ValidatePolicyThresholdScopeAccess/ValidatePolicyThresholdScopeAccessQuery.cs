using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.ValidatePolicyThresholdScopeAccess;

public sealed record ValidatePolicyThresholdScopeAccessQuery(
    string ScopeType,
    string ScopeKey,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<bool>>;
