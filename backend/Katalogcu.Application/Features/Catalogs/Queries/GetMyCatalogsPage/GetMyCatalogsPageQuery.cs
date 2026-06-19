using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogsPage;

public sealed record GetMyCatalogsPageQuery(
    Guid UserId,
    Guid? FolderId = null,
    int Page = 1,
    int PageSize = 24,
    string? Search = null) : IRequest<OperationResult<PagedCatalogListResponse>>;
