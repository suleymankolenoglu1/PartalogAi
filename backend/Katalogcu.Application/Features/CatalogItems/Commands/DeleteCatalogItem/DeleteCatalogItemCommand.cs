using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.DeleteCatalogItem;

public sealed record DeleteCatalogItemCommand(Guid CatalogItemId, Guid UserId) : IRequest<OperationResult<bool>>;
