using Katalogcu.Application.Features.Chat.Common;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IChatQueryService
{
    Task<IReadOnlyList<Guid>> ResolveAccessibleCatalogIdsAsync(
        Guid tokenUserId,
        Guid? publicUserId,
        IReadOnlyCollection<Guid>? publicAllowedCatalogIds,
        IReadOnlyCollection<Guid> requestedCatalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EnrichedPartDto>> EnrichPythonSourcesAsync(
        IReadOnlyCollection<ChatSourceInput> sources,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> SearchByCodeAsync(
        string? term,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EnrichedPartDto>> EnrichResultsAsync(
        IReadOnlyCollection<CatalogItem> items,
        IReadOnlyCollection<Guid> catalogIds,
        CancellationToken cancellationToken);
}
