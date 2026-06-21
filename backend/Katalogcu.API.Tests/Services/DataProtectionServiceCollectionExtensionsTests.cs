using Katalogcu.API.Services;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class DataProtectionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKatalogcuDataProtection_WiresRedisRepositoryAndEncryptor()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Provider"] = "Redis",
                ["DataProtection:RedisConnectionString"] = "redis.example:6379,abortConnect=false",
                ["DataProtection:RedisKey"] = "partalog:test:data-protection",
                ["DataProtection:KeyEncryptionKey"] = key
            })
            .Build();
        var services = new ServiceCollection();

        services.AddKatalogcuDataProtection(configuration);

        using var provider = services.BuildServiceProvider();
        var keyOptions = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.IsType<RedisDataProtectionXmlRepository>(keyOptions.XmlRepository);
        Assert.IsType<AesGcmDataProtectionXmlEncryptor>(keyOptions.XmlEncryptor);
        Assert.IsType<RedisDataProtectionXmlRepository>(provider.GetRequiredService<IXmlRepository>());
        Assert.IsType<AesGcmDataProtectionXmlEncryptor>(provider.GetRequiredService<IXmlEncryptor>());
    }
}
