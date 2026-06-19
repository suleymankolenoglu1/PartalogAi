using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyThresholds;

public sealed record GetPolicyThresholdsQuery(
    bool IncludeInactive,
    string? ScopeType,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<IReadOnlyList<PolicyThresholdDto>>>;
