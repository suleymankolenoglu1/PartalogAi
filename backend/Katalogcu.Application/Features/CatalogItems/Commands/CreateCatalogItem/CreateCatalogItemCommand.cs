using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.CatalogItems.Commands.CreateCatalogItem;

public sealed record CreateCatalogItemCommand(
    Guid CatalogId,
    int PageNumber,
    Guid UserId,
    string RefNo,
    string PartCode,
    string PartName,
    string? Description)
    : IRequest<OperationResult<CatalogPageItemDto>>;
