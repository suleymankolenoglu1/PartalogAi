using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.RejectCatalogExternalMatch;

public sealed record RejectCatalogExternalMatchCommand(Guid MatchId, string? ReviewNote)
    : IRequest<OperationResult<RejectCatalogExternalMatchResponse>>;

public sealed class RejectCatalogExternalMatchResponse
{
    public Guid MatchId { get; init; }
    public string Status { get; init; } = string.Empty;
}
