using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RestoreApprovedExternalMatch;

public sealed record RestoreApprovedExternalMatchCommand(Guid MatchId)
    : IRequest<OperationResult<RestoreApprovedExternalMatchResponse>>;

public sealed class RestoreApprovedExternalMatchResponse
{
    public Guid MatchId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool? IsLinkHealthy { get; init; }
}
