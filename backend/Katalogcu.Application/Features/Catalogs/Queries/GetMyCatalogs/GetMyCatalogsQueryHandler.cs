using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogs;

public sealed class GetMyCatalogsQueryHandler : IRequestHandler<GetMyCatalogsQuery, OperationResult<IReadOnlyList<Catalog>>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetMyCatalogsQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<IReadOnlyList<Catalog>>> Handle(GetMyCatalogsQuery request, CancellationToken cancellationToken)
    {
        var catalogs = await _catalogRepository.GetCatalogsByUserAsync(request.UserId, cancellationToken);
        return OperationResult<IReadOnlyList<Catalog>>.Success(catalogs);
    }
}
