using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinkByCatalogItem;

public sealed record GetPublishedExternalLinkByCatalogItemQuery(Guid CatalogItemId)
    : IRequest<OperationResult<GetPublishedExternalLinkByCatalogItemResponse>>;

public sealed class GetPublishedExternalLinkByCatalogItemResponse
{
    public PublishedExternalLinkDto? Link { get; init; }
}
