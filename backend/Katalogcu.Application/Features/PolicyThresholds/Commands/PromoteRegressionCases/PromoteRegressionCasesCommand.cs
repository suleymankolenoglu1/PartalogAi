using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.PromoteRegressionCases;

public sealed record PromoteRegressionCasesCommand(
    IReadOnlyCollection<PolicyRegressionCaseDraft> Cases,
    string? Note,
    PolicyThresholdActor Actor)
    : IRequest<OperationResult<PolicyRegressionPromotionResultDto>>;
