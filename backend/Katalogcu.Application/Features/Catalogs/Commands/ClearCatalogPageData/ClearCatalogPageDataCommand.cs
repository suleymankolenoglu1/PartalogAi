using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.ClearCatalogPageData;

public sealed record ClearCatalogPageDataCommand(Guid CatalogId, Guid PageId, Guid UserId)
    : IRequest<OperationResult<ClearCatalogPageDataResponse>>;

public sealed class ClearCatalogPageDataResponse
{
    public string Message { get; init; } = "Sayfa verileri temizlendi";
}
