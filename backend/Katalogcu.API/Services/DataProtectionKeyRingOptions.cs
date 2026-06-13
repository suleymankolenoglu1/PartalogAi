namespace Katalogcu.API.Services;

public sealed class DataProtectionKeyRingOptions
{
    public const string SectionName = "DataProtection";

    public string Provider { get; set; } = "";
    public string ApplicationName { get; set; } = "Katalogcu.API";
    public string KeysDirectory { get; set; } = "";
    public string RedisConnectionString { get; set; } = "";
    public string RedisKey { get; set; } = "partalog:data-protection:keys";
    public string KeyEncryptionKey { get; set; } = "";
}
