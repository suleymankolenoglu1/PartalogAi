using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.StartCatalogAiProcess;

public sealed record StartCatalogAiProcessCommand(Guid CatalogId, Guid UserId)
    : IRequest<OperationResult<StartCatalogAiProcessResponse>>;

public sealed class StartCatalogAiProcessResponse
{
    public string Message { get; init; } = "AI Analizi başlatıldı. İşlem bitince katalog otomatik olarak yayına alınacak.";
    public Guid CatalogId { get; init; }
    public string Status { get; init; } = "Processing";
}
