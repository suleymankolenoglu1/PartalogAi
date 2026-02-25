namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogPageFileService
{
    Task<byte[]?> ReadImageBytesAsync(string imageUrl, CancellationToken cancellationToken);
}
