using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogsByUser;

public sealed class GetPublicCatalogsByUserQueryHandler : IRequestHandler<GetPublicCatalogsByUserQuery, OperationResult<IReadOnlyList<Catalog>>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetPublicCatalogsByUserQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<IReadOnlyList<Catalog>>> Handle(GetPublicCatalogsByUserQuery request, CancellationToken cancellationToken)
    {
        var catalogs = await _catalogRepository.GetPublicCatalogsByUserAsync(
            request.UserId,
            request.AllowedCatalogIds,
            cancellationToken);

        return OperationResult<IReadOnlyList<Catalog>>.Success(catalogs);
    }
}
