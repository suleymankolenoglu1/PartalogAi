using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicToken;

public sealed record GetPublicTokenQuery(Guid UserId, IReadOnlyCollection<Guid> RequestedCatalogIds)
    : IRequest<OperationResult<string>>;
