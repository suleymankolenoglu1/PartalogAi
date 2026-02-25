using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.StartCatalogAiProcess;

public sealed class StartCatalogAiProcessCommandHandler : IRequestHandler<StartCatalogAiProcessCommand, OperationResult<StartCatalogAiProcessResponse>>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogAiBackgroundProcessor _catalogAiBackgroundProcessor;

    public StartCatalogAiProcessCommandHandler(
        ICatalogRepository catalogRepository,
        ICatalogAiBackgroundProcessor catalogAiBackgroundProcessor)
    {
        _catalogRepository = catalogRepository;
        _catalogAiBackgroundProcessor = catalogAiBackgroundProcessor;
    }

    public async Task<OperationResult<StartCatalogAiProcessResponse>> Handle(StartCatalogAiProcessCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetOwnedCatalogAsync(request.CatalogId, request.UserId, cancellationToken);
        if (catalog == null)
        {
            return OperationResult<StartCatalogAiProcessResponse>.Failure("not_found", "Katalog bulunamadı veya yetkiniz yok.");
        }

        if (catalog.Status == "Processing")
        {
            return OperationResult<StartCatalogAiProcessResponse>.Failure("validation", "Bu katalog zaten işleniyor.");
        }

        catalog.Status = "Processing";
        await _catalogRepository.SaveChangesAsync(cancellationToken);
        await _catalogAiBackgroundProcessor.EnqueueAsync(request.CatalogId, cancellationToken);

        return OperationResult<StartCatalogAiProcessResponse>.Success(new StartCatalogAiProcessResponse
        {
            CatalogId = request.CatalogId
        });
    }
}
