using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalProductByUrl;

public sealed record ApproveCatalogExternalProductByUrlCommand(
    Guid CatalogItemId,
    Guid ExternalSiteId,
    string ProductUrl,
    string? ProductTitle,
    string? ReviewNote) : IRequest<OperationResult<ApproveCatalogExternalProductByUrlResponse>>;

public sealed class ApproveCatalogExternalProductByUrlResponse
{
    public Guid MatchId { get; init; }
    public Guid CatalogItemId { get; init; }
    public Guid ExternalProductId { get; init; }
    public string Status { get; init; } = string.Empty;
}
