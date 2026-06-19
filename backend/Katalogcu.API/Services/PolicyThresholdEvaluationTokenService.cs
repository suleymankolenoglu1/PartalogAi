using System.Text.Json;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Katalogcu.API.Services;

public interface IPolicyThresholdEvaluationTokenService
{
    string CreateToken(
        PolicyThresholdRequestDto request,
        string scopeType,
        string scopeKey,
        Guid actorUserId,
        int caseCount,
        string? casesHash);

    bool TryApplyPolicyEvaluationToken(
        PolicyThresholdRequestDto request,
        string scopeType,
        string scopeKey,
        Guid actorUserId,
        out string? error);

    bool ValidateRegressionPromotionToken(
        string? evaluationToken,
        IReadOnlyCollection<PolicyRegressionCaseDraft> cases,
        Guid actorUserId,
        out string? error);
}

public sealed class PolicyThresholdEvaluationTokenService : IPolicyThresholdEvaluationTokenService
{
    private readonly IDataProtector _protector;

    public PolicyThresholdEvaluationTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("PolicyThresholds.EvalGate.v1");
    }

    public string CreateToken(
        PolicyThresholdRequestDto request,
        string scopeType,
        string scopeKey,
        Guid actorUserId,
        int caseCount,
        string? casesHash)
    {
        var payload = new PolicyEvalTokenPayload(
            actorUserId,
            scopeType,
            scopeKey,
            request.HighConfidence,
            request.LowConfidence,
            request.AmbiguityScoreDelta,
            caseCount,
            casesHash,
            DateTime.UtcNow.AddMinutes(30));

        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryApplyPolicyEvaluationToken(
        PolicyThresholdRequestDto request,
        string scopeType,
        string scopeKey,
        Guid actorUserId,
        out string? error)
    {
        if (!TryReadPayload(request.EvaluationToken, out var payload, out error))
        {
            return false;
        }

        if (payload!.ActorUserId != actorUserId ||
            !string.Equals(payload.ScopeType, scopeType, StringComparison.Ordinal) ||
            !string.Equals(payload.ScopeKey, scopeKey, StringComparison.Ordinal) ||
            payload.HighConfidence != request.HighConfidence ||
            payload.LowConfidence != request.LowConfidence ||
            payload.AmbiguityScoreDelta != request.AmbiguityScoreDelta)
        {
            error = "Eval token bu policy taslağıyla eşleşmiyor. Eval'i tekrar çalıştır.";
            return false;
        }

        if (payload.CaseCount <= 0)
        {
            error = "Eval token geçerli case içermiyor.";
            return false;
        }

        request.EvaluationPassed = true;
        request.EvaluationCaseCount = payload.CaseCount;
        return true;
    }

    public bool ValidateRegressionPromotionToken(
        string? evaluationToken,
        IReadOnlyCollection<PolicyRegressionCaseDraft> cases,
        Guid actorUserId,
        out string? error)
    {
        if (!TryReadPayload(evaluationToken, out var payload, out error))
        {
            if (string.IsNullOrWhiteSpace(evaluationToken))
            {
                error = "Regression set'e eklemeden önce geçerli bir eval token gerekli.";
            }

            return false;
        }

        if (payload!.ActorUserId != actorUserId)
        {
            error = "Eval token bu kullanıcıyla eşleşmiyor.";
            return false;
        }

        if (payload.CaseCount != cases.Count)
        {
            error = "Eval token case sayısı regression taslağıyla eşleşmiyor.";
            return false;
        }

        var casesHash = PolicyRegressionCaseParser.ComputeEvalCasesHash(cases.Select(x => x.EvalCase));
        if (string.IsNullOrWhiteSpace(payload.CasesHash) ||
            !string.Equals(payload.CasesHash, casesHash, StringComparison.Ordinal))
        {
            error = "Eval token bu regression taslağıyla eşleşmiyor. Eval'i tekrar çalıştır.";
            return false;
        }

        return true;
    }

    private bool TryReadPayload(
        string? evaluationToken,
        out PolicyEvalTokenPayload? payload,
        out string? error)
    {
        payload = null;
        error = null;
        if (string.IsNullOrWhiteSpace(evaluationToken))
        {
            error = "Policy aktifleşmeden önce geçerli bir eval token gerekli.";
            return false;
        }

        try
        {
            var json = _protector.Unprotect(evaluationToken);
            payload = JsonSerializer.Deserialize<PolicyEvalTokenPayload>(json);
        }
        catch
        {
            error = "Eval token doğrulanamadı.";
            return false;
        }

        if (payload is null)
        {
            error = "Eval token okunamadı.";
            return false;
        }

        if (payload.ExpiresAtUtc < DateTime.UtcNow)
        {
            error = "Eval token süresi doldu. Eval'i tekrar çalıştır.";
            return false;
        }

        return true;
    }

    private sealed record PolicyEvalTokenPayload(
        Guid ActorUserId,
        string ScopeType,
        string ScopeKey,
        decimal? HighConfidence,
        decimal? LowConfidence,
        decimal? AmbiguityScoreDelta,
        int CaseCount,
        string? CasesHash,
        DateTime ExpiresAtUtc);
}
