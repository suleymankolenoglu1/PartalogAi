using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalMatch;

public sealed record ApproveCatalogExternalMatchCommand(Guid MatchId, string? ReviewNote)
    : IRequest<OperationResult<ApproveCatalogExternalMatchResponse>>;

public sealed class ApproveCatalogExternalMatchResponse
{
    public Guid MatchId { get; init; }
    public Guid CatalogItemId { get; init; }
    public string Status { get; init; } = string.Empty;
}
