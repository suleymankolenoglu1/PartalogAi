using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.SetPolicyThresholdActive;

public sealed record SetPolicyThresholdActiveCommand(
    Guid Id,
    bool IsActive,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<PolicyThresholdDto>>;
