using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.CompleteCatalogUpload;

public sealed record CompleteCatalogUploadCommand(
    Guid CatalogId,
    Guid UserId)
    : IRequest<OperationResult<CompleteCatalogUploadResponse>>;

public sealed class CompleteCatalogUploadResponse
{
    public Guid CatalogId { get; init; }
    public string Status { get; init; } = "ReadyToProcess";
    public int PageCount { get; init; }
}
