using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinksByCatalog;

public sealed record GetPublishedExternalLinksByCatalogQuery(Guid CatalogId)
    : IRequest<OperationResult<GetPublishedExternalLinksByCatalogResponse>>;

public sealed class GetPublishedExternalLinksByCatalogResponse
{
    public IReadOnlyList<PublishedExternalLinkDto> Links { get; init; } = [];
}
