using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ReplaceCatalogExternalMatchCandidates;

public sealed record ReplaceCatalogExternalMatchCandidatesCommand(
    Guid CatalogId,
    Guid ExternalSiteId) : IRequest<OperationResult<ReplaceCatalogExternalMatchCandidatesResponse>>;

public sealed class ReplaceCatalogExternalMatchCandidatesResponse
{
    public Guid CatalogId { get; init; }
    public Guid ExternalSiteId { get; init; }
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
}
