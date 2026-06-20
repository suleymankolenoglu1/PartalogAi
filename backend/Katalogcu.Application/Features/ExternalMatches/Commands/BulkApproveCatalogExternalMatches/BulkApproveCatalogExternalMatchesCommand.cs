using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.BulkApproveCatalogExternalMatches;

public sealed record BulkApproveCatalogExternalMatchesCommand(
    IReadOnlyCollection<Guid> MatchIds,
    string? ReviewNote) : IRequest<OperationResult<BulkApproveCatalogExternalMatchesResponse>>;

public sealed class BulkApproveCatalogExternalMatchesResponse
{
    public int ApprovedCount { get; init; }
}
