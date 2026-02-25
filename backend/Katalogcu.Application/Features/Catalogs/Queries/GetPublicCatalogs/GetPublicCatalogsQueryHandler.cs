using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogs;

public sealed class GetPublicCatalogsQueryHandler : IRequestHandler<GetPublicCatalogsQuery, OperationResult<IReadOnlyList<Catalog>>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetPublicCatalogsQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<IReadOnlyList<Catalog>>> Handle(GetPublicCatalogsQuery request, CancellationToken cancellationToken)
    {
        var catalogs = await _catalogRepository.GetPublicCatalogsAsync(cancellationToken);
        return OperationResult<IReadOnlyList<Catalog>>.Success(catalogs);
    }
}
