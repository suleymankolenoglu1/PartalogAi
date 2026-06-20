using Katalogcu.Application.Features.ExternalSites.Common;
using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IManualImportService
{
    Task<ManualImportResultDto> ImportFromFileAsync(
        UploadedFile file,
        Guid externalSiteId,
        Guid userId,
        CancellationToken cancellationToken);
}
