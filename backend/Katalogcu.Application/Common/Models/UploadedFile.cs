namespace Katalogcu.Application.Common.Models;

public sealed class UploadedFile
{
    private readonly Func<Stream> _openReadStream;

    public UploadedFile(
        Func<Stream> openReadStream,
        string fileName,
        string name,
        string? contentType,
        long length)
    {
        _openReadStream = openReadStream ?? throw new ArgumentNullException(nameof(openReadStream));
        FileName = fileName;
        Name = name;
        ContentType = contentType;
        Length = length;
    }

    public string FileName { get; }
    public string Name { get; }
    public string? ContentType { get; }
    public long Length { get; }

    public Stream OpenReadStream() => _openReadStream();

    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        await using var source = OpenReadStream();
        await source.CopyToAsync(target, cancellationToken);
    }

    public static UploadedFile FromBytes(
        byte[] content,
        string fileName,
        string name = "file",
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new UploadedFile(
            () => new MemoryStream(content, writable: false),
            fileName,
            name,
            contentType,
            content.Length);
    }

    public static async Task<UploadedFile> FromStreamAsync(
        Stream source,
        string fileName,
        string name = "file",
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        return FromBytes(memory.ToArray(), fileName, name, contentType);
    }
}
