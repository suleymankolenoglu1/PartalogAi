using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogCoverMetadataService
{
    Task<CatalogCoverMetadataDto?> AnalyzeAsync(byte[] fileBytes, CancellationToken cancellationToken);
}
