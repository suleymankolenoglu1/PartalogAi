using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.EvaluatePolicyThreshold;

public sealed class EvaluatePolicyThresholdCommandHandler
    : IRequestHandler<EvaluatePolicyThresholdCommand, OperationResult<PolicyThresholdEvalResultDto>>
{
    private readonly IPartalogAiService _aiService;

    public EvaluatePolicyThresholdCommandHandler(IPartalogAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task<OperationResult<PolicyThresholdEvalResultDto>> Handle(
        EvaluatePolicyThresholdCommand request,
        CancellationToken cancellationToken)
    {
        var cases = request.Cases
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) || !string.IsNullOrWhiteSpace(x.Message))
            .Take(25)
            .ToList();
        if (cases.Count == 0)
        {
            return OperationResult<PolicyThresholdEvalResultDto>.Failure("validation", "Eval için en az bir case gerekli.");
        }

        var overrideJson = PolicyThresholdEvalHelpers.BuildPolicyOverrideJson(request.Policy, request.ScopeType, request.ScopeKey);
        var results = new List<PolicyThresholdEvalCaseResultDto>();
        var passedCount = 0;

        foreach (var evalCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var catalogIds = PolicyThresholdEvalHelpers.ResolveEvalCatalogIds(evalCase, request.ScopeType, request.ScopeKey);
            var aiResponse = await _aiService.GetExpertChatResponseAsync(new AiChatRequestDto
            {
                Text = evalCase.Text ?? evalCase.Message,
                History = [],
                CatalogIds = catalogIds,
                ContextJson = PolicyThresholdEvalHelpers.SerializeContext(evalCase.ContextJson),
                UserPlan = "Enterprise",
                AiLimitPerMonth = null,
                AiUsedThisMonth = 0,
                PolicyThresholdOverride = overrideJson
            });

            var sourceCodes = (aiResponse.Sources ?? [])
                .Select(x => x.Code)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            var reply = aiResponse.Answer ?? string.Empty;
            var mentionedCodes = PolicyThresholdEvalHelpers.ExtractCodes(reply);
            var allCodes = sourceCodes.Concat(mentionedCodes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var expectedCodes = PolicyRegressionCaseParser.NormalizeTerms(evalCase.ExpectedCodes);
            var requiredTerms = PolicyRegressionCaseParser.NormalizeTerms(evalCase.RequiredTerms);
            var forbiddenTerms = PolicyRegressionCaseParser.NormalizeTerms(evalCase.ForbiddenTerms);

            var expectedOk = expectedCodes.Count == 0 || expectedCodes.Any(x => allCodes.Contains(x, StringComparer.OrdinalIgnoreCase));
            var noCodesOk = !evalCase.ExpectNoCodes || allCodes.Count == 0;
            var requiredOk = requiredTerms.Count == 0 || requiredTerms.All(term => PolicyThresholdEvalHelpers.ContainsInvariant(reply, term));
            var forbiddenOk = forbiddenTerms.Count == 0 || forbiddenTerms.All(term => !PolicyThresholdEvalHelpers.ContainsInvariant(reply, term));
            var ok = expectedOk && noCodesOk && requiredOk && forbiddenOk;
            if (ok)
            {
                passedCount++;
            }

            results.Add(new PolicyThresholdEvalCaseResultDto
            {
                Id = string.IsNullOrWhiteSpace(evalCase.Id) ? $"case-{results.Count + 1}" : evalCase.Id,
                Ok = ok,
                ExpectedOk = expectedOk,
                NoCodesOk = noCodesOk,
                RequiredOk = requiredOk,
                ForbiddenOk = forbiddenOk,
                Codes = allCodes.Take(8).ToList(),
                AnswerPreview = PolicyThresholdEvalHelpers.TrimPreview(reply)
            });
        }

        var passed = passedCount == cases.Count;
        return OperationResult<PolicyThresholdEvalResultDto>.Success(new PolicyThresholdEvalResultDto
        {
            Passed = passed,
            Total = cases.Count,
            PassedCount = passedCount,
            FailedCount = cases.Count - passedCount,
            PassRate = cases.Count == 0 ? 0 : Math.Round((double)passedCount / cases.Count, 4),
            ThresholdSource = $"candidate:{request.ScopeType.ToLowerInvariant()}:{request.ScopeKey}",
            CasesHash = passed ? PolicyRegressionCaseParser.ComputeEvalCasesHash(cases) : null,
            Results = results
        });
    }
}
