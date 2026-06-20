using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RefreshApprovedExternalLinkHealth;

public sealed record RefreshApprovedExternalLinkHealthCommand(Guid MatchId)
    : IRequest<OperationResult<RefreshApprovedExternalLinkHealthResponse>>;

public sealed class RefreshApprovedExternalLinkHealthResponse
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
