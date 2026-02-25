using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.CreateCatalog;

public sealed record CreateCatalogCommand(
    Guid UserId,
    string Name,
    string? Description,
    string? PdfUrl,
    string? ImageUrl,
    Guid? FolderId)
    : IRequest<OperationResult<Catalog>>;
