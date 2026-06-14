using Katalogcu.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class ProductionConfigurationGuardTests
{
    [Fact]
    public void Check_BlocksProductionWhenRequiredSecretsArePlaceholders()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "CHANGE_ME_CHANGE_ME_CHANGE_ME_CHANGE_ME",
            ["PublicLink:SecretKey"] = "catalog_only_local_public_secret_32_chars_minimum",
            ["EmbedAccessToken:SecretKey"] = "catalog_only_local_embed_secret_32_chars_minimum",
            ["ConnectionStrings:DefaultConnection"] = "Host=db.example.internal;Database=Katalogcu;Username=app;Password=secret;Pooling=true;Maximum Pool Size=20;Timeout=15;Command Timeout=30;Max Auto Prepare=0"
        });

        var checks = ProductionConfigurationGuard.Check(
            configuration,
            isProduction: true,
            aiServiceRequired: true,
            new AiServiceOptions { BaseUrl = "https://partalog-ai.example.com" },
            new DataProtectionKeyRingOptions
            {
                Provider = "Redis",
                RedisConnectionString = "redis.example.internal:6379,abortConnect=false",
                KeyEncryptionKey = "not-base64"
            });

        var requiredSecrets = Assert.Single(checks, c => c.Name == "production_required_secrets");
        Assert.Equal("fail", requiredSecrets.Status);

        var embedSecret = Assert.Single(checks, c => c.Name == "embed_access_token_secret");
        Assert.Equal("fail", embedSecret.Status);
    }

    [Fact]
    public void Check_BlocksProductionWhenAiServiceUsesLoopback()
    {
        var configuration = BuildValidProductionConfiguration();

        var checks = ProductionConfigurationGuard.Check(
            configuration,
            isProduction: true,
            aiServiceRequired: true,
            new AiServiceOptions { BaseUrl = "http://127.0.0.1:8000" },
            ValidDataProtectionOptions());

        var aiEndpoint = Assert.Single(checks, c => c.Name == "ai_service_endpoint_config");
        Assert.Equal("fail", aiEndpoint.Status);
    }

    [Fact]
    public void Check_PassesValidProductionConfiguration()
    {
        var configuration = BuildValidProductionConfiguration();

        var checks = ProductionConfigurationGuard.Check(
            configuration,
            isProduction: true,
            aiServiceRequired: true,
            new AiServiceOptions { BaseUrl = "https://partalog-ai.example.com" },
            ValidDataProtectionOptions());

        Assert.DoesNotContain(checks, check => check.Status == "fail");
        Assert.Contains(checks, check => check.Name == "production_required_secrets" && check.Status == "pass");
        Assert.Contains(checks, check => check.Name == "embed_access_token_secret" && check.Status == "pass");
        Assert.Contains(checks, check => check.Name == "ai_service_endpoint_config" && check.Status == "pass");
    }

    private static IConfiguration BuildValidProductionConfiguration()
    {
        return BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "prod-jwt-secret-value-with-more-than-32-chars",
            ["PublicLink:SecretKey"] = "prod-public-link-secret-more-than-32-chars",
            ["EmbedAccessToken:SecretKey"] = "prod-embed-token-secret-more-than-32-chars",
            ["ConnectionStrings:DefaultConnection"] = "Host=db.example.internal;Database=Katalogcu;Username=app;Password=secret;Pooling=true;Maximum Pool Size=20;Timeout=15;Command Timeout=30;Max Auto Prepare=0",
            ["DistributedRateLimits:RedisConnectionString"] = "redis.example.internal:6379,abortConnect=false",
            ["AiCapacity:RedisConnectionString"] = "redis.example.internal:6379,abortConnect=false"
        });
    }

    private static DataProtectionKeyRingOptions ValidDataProtectionOptions()
    {
        return new DataProtectionKeyRingOptions
        {
            Provider = "Redis",
            RedisConnectionString = "redis.example.internal:6379,abortConnect=false",
            KeyEncryptionKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray())
        };
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
