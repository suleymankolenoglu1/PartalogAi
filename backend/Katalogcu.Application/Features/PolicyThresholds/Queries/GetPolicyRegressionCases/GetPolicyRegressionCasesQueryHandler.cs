using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyRegressionCases;

public sealed class GetPolicyRegressionCasesQueryHandler
    : IRequestHandler<GetPolicyRegressionCasesQuery, OperationResult<PolicyRegressionCasePreviewResultDto>>
{
    private readonly IPolicyRegressionCaseStore _caseStore;

    public GetPolicyRegressionCasesQueryHandler(IPolicyRegressionCaseStore caseStore)
    {
        _caseStore = caseStore;
    }

    public async Task<OperationResult<PolicyRegressionCasePreviewResultDto>> Handle(
        GetPolicyRegressionCasesQuery request,
        CancellationToken cancellationToken)
    {
        var preview = await _caseStore.GetPreviewAsync(
            Math.Clamp(request.Take, 1, 100),
            cancellationToken);

        return OperationResult<PolicyRegressionCasePreviewResultDto>.Success(preview);
    }
}
