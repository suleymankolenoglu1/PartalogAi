using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogsByUser;

public sealed record GetPublicCatalogsByUserQuery(Guid UserId, IReadOnlyCollection<Guid>? AllowedCatalogIds)
    : IRequest<OperationResult<IReadOnlyList<Catalog>>>;
