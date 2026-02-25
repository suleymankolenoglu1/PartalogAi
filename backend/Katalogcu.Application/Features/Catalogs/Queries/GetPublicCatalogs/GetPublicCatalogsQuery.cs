using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogs;

public sealed record GetPublicCatalogsQuery : IRequest<OperationResult<IReadOnlyList<Catalog>>>;
