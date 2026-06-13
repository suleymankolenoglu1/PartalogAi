namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public class PolicyThresholdRequestDto
{
    public string ScopeType { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public decimal? HighConfidence { get; set; }
    public decimal? LowConfidence { get; set; }
    public decimal? AmbiguityScoreDelta { get; set; }
    public string? Notes { get; set; }
    public bool RequireEvaluation { get; set; } = true;
    public bool EvaluationPassed { get; set; }
    public int EvaluationCaseCount { get; set; }
    public string? EvaluationToken { get; set; }
}

public sealed class PolicyThresholdDto
{
    public Guid Id { get; init; }
    public string ScopeType { get; init; } = string.Empty;
    public string ScopeKey { get; init; } = string.Empty;
    public decimal? HighConfidence { get; init; }
    public decimal? LowConfidence { get; init; }
    public decimal? AmbiguityScoreDelta { get; init; }
    public bool IsActive { get; init; }
    public int Version { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class PolicyThresholdListResponseDto
{
    public IReadOnlyList<PolicyThresholdDto> Items { get; init; } = [];
}

public sealed record PolicyThresholdActor(
    Guid UserId,
    bool IsPlatformAdmin,
    string? Email,
    string? Role,
    string? IpAddress,
    string? UserAgent);

public sealed class PolicyThresholdOperationDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ScopeType { get; init; }
    public string? ScopeKey { get; init; }
    public string ScopeLabel { get; init; } = string.Empty;
    public int? EvaluationCaseCount { get; init; }
    public int? PromotedCaseCount { get; init; }
    public int? SkippedCaseCount { get; init; }
    public string? Note { get; init; }
}

public sealed class PolicyThresholdOperationsResponseDto
{
    public IReadOnlyList<PolicyThresholdOperationDto> Items { get; init; } = [];
}

public sealed class PolicyThresholdOperationLog
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public DateTime CreatedDate { get; init; }
    public string? Payload { get; init; }
}

public sealed record PolicyAuditScope(string? ScopeType, string? ScopeKey);

public class PolicyThresholdEvalCaseDto
{
    public string? Id { get; set; }
    public string? Text { get; set; }
    public string? Message { get; set; }
    public List<string>? CatalogIds { get; set; }
    public object? ContextJson { get; set; }
    public List<string>? ExpectedCodes { get; set; }
    public List<string>? RequiredTerms { get; set; }
    public List<string>? ForbiddenTerms { get; set; }
    public bool ExpectNoCodes { get; set; }
}

public sealed record PolicyRegressionCaseDraft(
    string Id,
    string CanonicalJson,
    PolicyThresholdEvalCaseDto EvalCase);

public sealed class PolicyRegressionPromotionResultDto
{
    public bool Success { get; init; } = true;
    public int Appended { get; init; }
    public int Skipped { get; init; }
    public int Requested { get; init; }
    public string Path { get; init; } = string.Empty;
    public IReadOnlyList<string> CaseIds { get; init; } = [];
}

public sealed class PolicyRegressionCasePreviewItemDto
{
    public int LineNumber { get; init; }
    public string? Id { get; init; }
    public string? Text { get; init; }
    public string? Message { get; init; }
    public string? FeedbackId { get; init; }
    public string? FeedbackReason { get; init; }
    public IReadOnlyList<string> CatalogIds { get; init; } = [];
    public IReadOnlyList<string> ExpectedCodes { get; init; } = [];
    public IReadOnlyList<string> RequiredTerms { get; init; } = [];
    public IReadOnlyList<string> ForbiddenTerms { get; init; } = [];
    public bool ExpectNoCodes { get; init; }
    public bool HasContext { get; init; }
}

public sealed class PolicyRegressionCasePreviewResultDto
{
    public IReadOnlyList<PolicyRegressionCasePreviewItemDto> Items { get; init; } = [];
    public int Total { get; init; }
    public string Path { get; init; } = string.Empty;
}

public sealed class PolicyThresholdEvalCaseResultDto
{
    public string Id { get; init; } = string.Empty;
    public bool Ok { get; init; }
    public bool ExpectedOk { get; init; }
    public bool NoCodesOk { get; init; }
    public bool RequiredOk { get; init; }
    public bool ForbiddenOk { get; init; }
    public IReadOnlyList<string> Codes { get; init; } = [];
    public string AnswerPreview { get; init; } = string.Empty;
}

public sealed class PolicyThresholdEvalResultDto
{
    public bool Passed { get; init; }
    public int Total { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public double PassRate { get; init; }
    public string ThresholdSource { get; init; } = string.Empty;
    public string? CasesHash { get; init; }
    public IReadOnlyList<PolicyThresholdEvalCaseResultDto> Results { get; init; } = [];
}

public sealed class PolicyThresholdEvalResponseDto
{
    public bool Passed { get; init; }
    public int Total { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public double PassRate { get; init; }
    public string ThresholdSource { get; init; } = string.Empty;
    public string? EvaluationToken { get; init; }
    public IReadOnlyList<PolicyThresholdEvalCaseResultDto> Results { get; init; } = [];
}
