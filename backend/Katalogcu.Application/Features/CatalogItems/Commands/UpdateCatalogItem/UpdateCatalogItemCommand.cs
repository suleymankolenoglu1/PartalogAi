using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.UpdateCatalogItem;

public sealed record UpdateCatalogItemCommand(
    Guid CatalogItemId,
    Guid UserId,
    string RefNo,
    string PartCode,
    string PartName,
    string? Description)
    : IRequest<OperationResult<CatalogPageItemDto>>;
