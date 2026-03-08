using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IHotspotDetectionService
{
    Task<IReadOnlyList<Hotspot>> DetectHotspotsForPageAsync(
        string pageImageUrl,
        Guid pageId,
        CancellationToken cancellationToken,
        bool throwOnFailure = false);
}
