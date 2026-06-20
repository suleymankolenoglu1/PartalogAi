namespace Katalogcu.API.Services;

public sealed class StoredFile
{
    public StoredFile(Stream stream, string? contentType)
    {
        Stream = stream;
        ContentType = contentType;
    }

    public Stream Stream { get; }
    public string? ContentType { get; }
}
