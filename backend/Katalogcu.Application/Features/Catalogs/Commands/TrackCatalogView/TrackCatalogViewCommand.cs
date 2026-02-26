using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.TrackCatalogView;

public sealed record TrackCatalogViewCommand(
    Guid CatalogId,
    Guid OwnerUserId,
    string FingerprintHash,
    DateTime ViewedAtUtc,
    string Source)
    : IRequest<OperationResult<TrackCatalogViewResponse>>;

public sealed class TrackCatalogViewResponse
{
    public bool Recorded { get; init; }
}
