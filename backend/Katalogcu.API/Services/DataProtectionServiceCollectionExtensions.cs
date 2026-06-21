using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Katalogcu.API.Services;

public static class DataProtectionServiceCollectionExtensions
{
    public static IDataProtectionBuilder AddKatalogcuDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(DataProtectionKeyRingOptions.SectionName)
            .Get<DataProtectionKeyRingOptions>() ?? new DataProtectionKeyRingOptions();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(string.IsNullOrWhiteSpace(options.ApplicationName)
                ? "Katalogcu.API"
                : options.ApplicationName.Trim());

        var provider = (options.Provider ?? string.Empty).Trim();
        if (provider.Equals("Redis", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            services.AddSingleton<IXmlRepository>(new RedisDataProtectionXmlRepository(
                options.RedisConnectionString,
                options.RedisKey));
            services.AddOptions<KeyManagementOptions>()
                .Configure<IXmlRepository>((keyOptions, repository) =>
                {
                    keyOptions.XmlRepository = repository;
                });
        }
        else if (provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase)
                 && !string.IsNullOrWhiteSpace(options.KeysDirectory))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(options.KeysDirectory));
        }

        if (AesGcmDataProtectionXmlEncryptor.IsConfigured(options.KeyEncryptionKey))
        {
            services.AddSingleton<IXmlEncryptor>(new AesGcmDataProtectionXmlEncryptor(options.KeyEncryptionKey));
            services.AddOptions<KeyManagementOptions>()
                .Configure<IXmlEncryptor>((keyOptions, encryptor) =>
                {
                    keyOptions.XmlEncryptor = encryptor;
                });
        }

        return builder;
    }
}
