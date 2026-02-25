using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    Guid CatalogId,
    string Name,
    string Code,
    string? OemNo,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    string? Category,
    string? Description,
    string? PageNumber,
    int RefNo)
    : IRequest<OperationResult<CreateProductResponse>>;
