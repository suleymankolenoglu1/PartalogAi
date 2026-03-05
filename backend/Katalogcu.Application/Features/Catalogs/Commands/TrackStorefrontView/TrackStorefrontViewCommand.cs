using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.TrackStorefrontView;

public sealed record TrackStorefrontViewCommand(
    Guid OwnerUserId,
    string FingerprintHash,
    DateTime ViewedAtUtc,
    string Source)
    : IRequest<OperationResult<TrackStorefrontViewResponse>>;

public sealed class TrackStorefrontViewResponse
{
    public bool Recorded { get; init; }
}
