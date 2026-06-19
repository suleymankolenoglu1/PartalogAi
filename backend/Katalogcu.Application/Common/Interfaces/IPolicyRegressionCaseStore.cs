using Katalogcu.Application.Features.PolicyThresholds.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPolicyRegressionCaseStore
{
    Task<PolicyRegressionCaseStoreResult> PromoteAsync(
        IReadOnlyCollection<PolicyRegressionCaseDraft> cases,
        string? note,
        string actorEmail,
        CancellationToken cancellationToken);

    Task<PolicyRegressionCasePreviewResultDto> GetPreviewAsync(
        int take,
        CancellationToken cancellationToken);
}

public sealed class PolicyRegressionCaseStoreResult
{
    public int Appended { get; init; }
    public int Skipped { get; init; }
    public string Path { get; init; } = string.Empty;
    public IReadOnlyList<string> AppendedCaseIds { get; init; } = [];
}
