using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyOperations;

public sealed record GetPolicyOperationsQuery(
    int Take,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<IReadOnlyList<PolicyThresholdOperationDto>>>;
