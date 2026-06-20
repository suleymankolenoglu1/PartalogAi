namespace Katalogcu.API.Services;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Local";
    public string? LocalRootPath { get; set; }
    public string? BucketName { get; set; }
    public string? PublicBaseUrl { get; set; }
}
