using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.MarkApprovedExternalMatchBroken;

public sealed record MarkApprovedExternalMatchBrokenCommand(Guid MatchId)
    : IRequest<OperationResult<MarkApprovedExternalMatchBrokenResponse>>;

public sealed class MarkApprovedExternalMatchBrokenResponse
{
    public Guid MatchId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool? IsLinkHealthy { get; init; }
}
