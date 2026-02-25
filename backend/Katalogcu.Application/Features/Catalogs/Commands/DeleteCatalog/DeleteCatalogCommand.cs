using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.DeleteCatalog;

public sealed record DeleteCatalogCommand(Guid CatalogId, Guid UserId) : IRequest<OperationResult<bool>>;
