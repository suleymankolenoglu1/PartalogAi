using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.EvaluatePolicyThreshold;

public sealed record EvaluatePolicyThresholdCommand(
    PolicyThresholdRequestDto Policy,
    IReadOnlyCollection<PolicyThresholdEvalCaseDto> Cases,
    string ScopeType,
    string ScopeKey)
    : IRequest<OperationResult<PolicyThresholdEvalResultDto>>;
