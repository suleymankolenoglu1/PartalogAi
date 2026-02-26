using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.TrackCatalogView;

public sealed class TrackCatalogViewCommandHandler : IRequestHandler<TrackCatalogViewCommand, OperationResult<TrackCatalogViewResponse>>
{
    private static readonly TimeSpan BucketSize = TimeSpan.FromMinutes(30);
    private readonly ICatalogRepository _catalogRepository;

    public TrackCatalogViewCommandHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<TrackCatalogViewResponse>> Handle(TrackCatalogViewCommand request, CancellationToken cancellationToken)
    {
        var viewedAtUtc = request.ViewedAtUtc.Kind == DateTimeKind.Utc
            ? request.ViewedAtUtc
            : request.ViewedAtUtc.ToUniversalTime();
        var bucketStartUtc = AlignToBucket(viewedAtUtc, BucketSize);

        var recorded = await _catalogRepository.RecordCatalogViewAsync(
            request.CatalogId,
            request.OwnerUserId,
            request.FingerprintHash.Trim(),
            bucketStartUtc,
            viewedAtUtc,
            request.Source.Trim(),
            cancellationToken);

        return OperationResult<TrackCatalogViewResponse>.Success(new TrackCatalogViewResponse
        {
            Recorded = recorded
        });
    }

    private static DateTime AlignToBucket(DateTime utcValue, TimeSpan bucketSize)
    {
        var ticks = utcValue.Ticks - (utcValue.Ticks % bucketSize.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
