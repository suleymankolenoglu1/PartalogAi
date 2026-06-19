using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyRegressionCases;

public sealed record GetPolicyRegressionCasesQuery(int Take)
    : IRequest<OperationResult<PolicyRegressionCasePreviewResultDto>>;
