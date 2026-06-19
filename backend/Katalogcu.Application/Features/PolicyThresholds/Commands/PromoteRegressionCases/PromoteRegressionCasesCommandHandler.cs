using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.PromoteRegressionCases;

public sealed class PromoteRegressionCasesCommandHandler
    : IRequestHandler<PromoteRegressionCasesCommand, OperationResult<PolicyRegressionPromotionResultDto>>
{
    private readonly IPolicyRegressionCaseStore _caseStore;
    private readonly IPolicyThresholdRepository _repository;
    private readonly IPolicyThresholdAuditWriter _auditWriter;

    public PromoteRegressionCasesCommandHandler(
        IPolicyRegressionCaseStore caseStore,
        IPolicyThresholdRepository repository,
        IPolicyThresholdAuditWriter auditWriter)
    {
        _caseStore = caseStore;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<OperationResult<PolicyRegressionPromotionResultDto>> Handle(
        PromoteRegressionCasesCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Cases.Count == 0)
        {
            return OperationResult<PolicyRegressionPromotionResultDto>.Failure(
                "validation",
                "Regression set'e eklenecek geçerli eval case bulunamadı.");
        }

        var note = PolicyThresholdRules.NormalizeOptional(request.Note, 240);
        var storeResult = await _caseStore.PromoteAsync(
            request.Cases,
            note,
            request.Actor.Email ?? "admin",
            cancellationToken);

        _auditWriter.AddAuditLog(
            request.Actor,
            "PolicyThreshold.RegressionCasesPromoted",
            null,
            null,
            new
            {
                path = storeResult.Path,
                appended = storeResult.Appended,
                skipped = storeResult.Skipped,
                requested = request.Cases.Count,
                caseIds = storeResult.AppendedCaseIds.Take(25).ToList(),
                note
            });
        await _repository.SaveChangesAsync(cancellationToken);

        return OperationResult<PolicyRegressionPromotionResultDto>.Success(new PolicyRegressionPromotionResultDto
        {
            Success = true,
            Appended = storeResult.Appended,
            Skipped = storeResult.Skipped,
            Requested = request.Cases.Count,
            Path = storeResult.Path,
            CaseIds = storeResult.AppendedCaseIds
        });
    }
}
