using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogById;

public sealed class GetCatalogByIdQueryHandler : IRequestHandler<GetCatalogByIdQuery, OperationResult<Catalog>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetCatalogByIdQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<Catalog>> Handle(GetCatalogByIdQuery request, CancellationToken cancellationToken)
    {
        var catalog = await _catalogRepository.GetCatalogByIdForAccessAsync(
            request.CatalogId,
            request.UserId,
            request.IsPublic,
            request.AllowedCatalogIds,
            cancellationToken);

        if (catalog == null)
        {
            return OperationResult<Catalog>.Failure("not_found", "Katalog bulunamadı.");
        }

        return OperationResult<Catalog>.Success(catalog);
    }
}
