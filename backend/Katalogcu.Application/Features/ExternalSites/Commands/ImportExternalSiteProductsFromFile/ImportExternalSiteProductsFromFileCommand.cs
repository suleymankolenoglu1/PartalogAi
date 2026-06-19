using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.ImportExternalSiteProductsFromFile;

public sealed record ImportExternalSiteProductsFromFileCommand(Guid SiteId, UploadedFile File, string? FileType)
    : IRequest<OperationResult<ManualImportResultDto>>;
