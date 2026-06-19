namespace Katalogcu.API.Services;

public interface IFileStorageService
{
    Task SaveAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken);
    Task<StoredFile?> OpenReadAsync(string objectPath, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken);
    string? GetPublicUrl(string objectPath);
    bool TryGetObjectPath(string? urlOrPath, out string objectPath);
}
