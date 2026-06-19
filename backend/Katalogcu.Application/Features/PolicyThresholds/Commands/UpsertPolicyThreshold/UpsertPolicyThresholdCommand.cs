using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.UpsertPolicyThreshold;

public sealed record UpsertPolicyThresholdCommand(
    Guid? Id,
    PolicyThresholdRequestDto Request,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<PolicyThresholdDto>>;
