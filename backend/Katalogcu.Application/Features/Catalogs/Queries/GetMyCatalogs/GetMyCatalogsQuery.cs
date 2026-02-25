using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogs;

public sealed record GetMyCatalogsQuery(Guid UserId) : IRequest<OperationResult<IReadOnlyList<Catalog>>>;
