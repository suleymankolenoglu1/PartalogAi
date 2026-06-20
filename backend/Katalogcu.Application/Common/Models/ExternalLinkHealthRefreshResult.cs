namespace Katalogcu.Application.Common.Models;

public sealed class ExternalLinkHealthRefreshResult
{
    public Guid MatchId { get; init; }
    public Guid ExternalProductId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsReachable { get; init; }
    public int? StatusCode { get; init; }
    public DateTime CheckedAtUtc { get; init; }
    public string? FinalUrl { get; init; }
    public string? ErrorSummary { get; init; }
}
