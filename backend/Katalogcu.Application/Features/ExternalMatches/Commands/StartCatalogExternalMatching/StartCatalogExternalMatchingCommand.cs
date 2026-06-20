using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.StartCatalogExternalMatching;

public sealed record StartCatalogExternalMatchingCommand(Guid CatalogId, Guid ExternalSiteId)
    : IRequest<OperationResult<StartCatalogExternalMatchingResponse>>;

public sealed class StartCatalogExternalMatchingResponse
{
    public Guid CatalogId { get; init; }
    public Guid ExternalSiteId { get; init; }
    public int CandidateCount { get; init; }
}
