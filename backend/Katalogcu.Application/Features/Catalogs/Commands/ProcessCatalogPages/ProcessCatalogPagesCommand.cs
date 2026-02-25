using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.ProcessCatalogPages;

public sealed record ProcessCatalogPagesCommand(Guid CatalogId)
    : IRequest<OperationResult<ProcessCatalogPagesResponse>>;

public sealed class ProcessCatalogPagesResponse
{
    public Guid CatalogId { get; init; }
    public int ProcessedPageCount { get; init; }
    public int FailedPageCount { get; init; }
    public int SavedItemCount { get; init; }
    public int SavedHotspotCount { get; init; }
    public string Status { get; init; } = "Published";
}
