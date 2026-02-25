using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.FailCatalogUpload;

public sealed record FailCatalogUploadCommand(Guid CatalogId, Guid UserId)
    : IRequest<OperationResult<FailCatalogUploadResponse>>;

public sealed class FailCatalogUploadResponse
{
    public Guid CatalogId { get; init; }
    public string Status { get; init; } = "Error";
}
