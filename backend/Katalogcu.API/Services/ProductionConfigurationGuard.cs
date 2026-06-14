using System.Net;

namespace Katalogcu.API.Services;

public static class ProductionConfigurationGuard
{
    public static IReadOnlyList<ProductionReadinessCheck> Check(
        IConfiguration configuration,
        bool isProduction,
        bool aiServiceRequired,
        AiServiceOptions aiServiceOptions,
        DataProtectionKeyRingOptions dataProtectionOptions)
    {
        return
        [
            CheckRequiredSecrets(configuration, isProduction, dataProtectionOptions),
            CheckEmbedAccessTokenSecret(configuration, isProduction),
            CheckAiServiceEndpoint(isProduction, aiServiceRequired, aiServiceOptions),
            CheckRuntimeConnectionTargets(configuration, isProduction, dataProtectionOptions)
        ];
    }

    private static ProductionReadinessCheck CheckRequiredSecrets(
        IConfiguration configuration,
        bool isProduction,
        DataProtectionKeyRingOptions dataProtectionOptions)
    {
        var invalidSecrets = new List<object>();
        AddInvalidSecretIfNeeded(
            invalidSecrets,
            "JwtSettings:SecretKey",
            JwtSecretResolver.Resolve(configuration),
            minLength: 32);
        AddInvalidSecretIfNeeded(
            invalidSecrets,
            "PublicLink:SecretKey",
            FirstNonEmpty(configuration["PublicLink:SecretKey"], configuration["PublicLinkSecret"]),
            minLength: 32);

        var keyEncryptionResult = ValidateDataProtectionKeyEncryptionKey(dataProtectionOptions.KeyEncryptionKey);
        if (!keyEncryptionResult.Valid)
        {
            invalidSecrets.Add(new
            {
                name = "DataProtection:KeyEncryptionKey",
                reason = keyEncryptionResult.Reason
            });
        }

        if (invalidSecrets.Count == 0)
        {
            return Pass(
                "production_required_secrets",
                "JWT, public link ve DataProtection encryption secret kontrolleri geçti.",
                new { checkedSecrets = 3 });
        }

        return isProduction
            ? Fail(
                "production_required_secrets",
                "Production için zorunlu secret/config değerleri eksik, placeholder veya geçersiz.",
                new { invalidSecrets })
            : Warn(
                "production_required_secrets",
                "Local/dev secret değerleri prod standardında değil; deploy öncesi Secret Manager değerleriyle doğrulanmalı.",
                new { invalidSecrets });
    }

    private static ProductionReadinessCheck CheckEmbedAccessTokenSecret(
        IConfiguration configuration,
        bool isProduction)
    {
        var embedSecret = FirstNonEmpty(configuration["EmbedAccessToken:SecretKey"]);
        var publicLinkSecret = FirstNonEmpty(configuration["PublicLink:SecretKey"], configuration["PublicLinkSecret"]);
        var hasDedicatedEmbedSecret = IsStrongSecret(embedSecret, minLength: 32);

        if (hasDedicatedEmbedSecret)
        {
            return Pass("embed_access_token_secret", "EmbedAccessToken dedicated secret ile yapılandırılmış.");
        }

        if (IsStrongSecret(publicLinkSecret, minLength: 32))
        {
            return Warn(
                "embed_access_token_secret",
                "EmbedAccessToken:SecretKey boş; runtime PublicLink secret fallback'ine düşer. Prod için ayrı secret önerilir.");
        }

        return isProduction
            ? Fail("embed_access_token_secret", "Production için EmbedAccessToken veya PublicLink secret geçerli değil.")
            : Warn("embed_access_token_secret", "Embed token secret local/dev ortamda prod standardında değil.");
    }

    private static ProductionReadinessCheck CheckAiServiceEndpoint(
        bool isProduction,
        bool aiServiceRequired,
        AiServiceOptions aiServiceOptions)
    {
        if (!aiServiceRequired)
        {
            return Pass("ai_service_endpoint_config", "AI modülleri kapalı; AI service endpoint zorunlu değil.");
        }

        if (!Uri.TryCreate(aiServiceOptions.BaseUrl, UriKind.Absolute, out var uri))
        {
            return Fail("ai_service_endpoint_config", "AiService:BaseUrl geçerli bir absolute URL değil.");
        }

        if (IsLoopbackHost(uri.Host))
        {
            return isProduction
                ? Fail(
                    "ai_service_endpoint_config",
                    "Production AI modülü açık ama AiService:BaseUrl loopback/localhost görünüyor.",
                    new { host = uri.Host })
                : Pass(
                    "ai_service_endpoint_config",
                    "AI service endpoint local/dev loopback kullanıyor.",
                    new { host = uri.Host });
        }

        if (isProduction && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return Warn(
                "ai_service_endpoint_config",
                "Production AI service endpoint HTTP kullanıyor. İç ağ dışında HTTPS tercih edilmeli.",
                new { scheme = uri.Scheme, host = uri.Host });
        }

        return Pass("ai_service_endpoint_config", "AI service endpoint prod için local fallback kullanmıyor.", new { uri.Scheme, uri.Host });
    }

    private static ProductionReadinessCheck CheckRuntimeConnectionTargets(
        IConfiguration configuration,
        bool isProduction,
        DataProtectionKeyRingOptions dataProtectionOptions)
    {
        var localTargets = new List<object>();
        AddLocalTargetIfNeeded(localTargets, "ConnectionStrings:DefaultConnection", GetConnectionString(configuration));
        AddLocalTargetIfNeeded(localTargets, "DataProtection:RedisConnectionString", dataProtectionOptions.RedisConnectionString);
        AddLocalTargetIfNeeded(localTargets, "DistributedRateLimits:RedisConnectionString", configuration["DistributedRateLimits:RedisConnectionString"]);
        AddLocalTargetIfNeeded(localTargets, "AiCapacity:RedisConnectionString", configuration["AiCapacity:RedisConnectionString"]);

        if (localTargets.Count == 0)
        {
            return Pass("production_connection_targets", "DB/Redis connection target değerlerinde localhost fallback görünmüyor.");
        }

        return isProduction
            ? Warn(
                "production_connection_targets",
                "Production config içinde localhost/loopback connection target görünüyor. Sidecar değilse deploy öncesi düzeltilmeli.",
                new { localTargets })
            : Pass(
                "production_connection_targets",
                "Local/dev connection target değerleri localhost kullanabilir.",
                new { localTargets });
    }

    private static void AddInvalidSecretIfNeeded(
        List<object> invalidSecrets,
        string name,
        string? value,
        int minLength)
    {
        var reason = GetSecretProblem(value, minLength);
        if (reason is not null)
        {
            invalidSecrets.Add(new { name, reason });
        }
    }

    private static string? GetSecretProblem(string? value, int minLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        var trimmed = value.Trim();
        if (trimmed.Length < minLength)
        {
            return $"shorter_than_{minLength}";
        }

        return LooksLikePlaceholder(trimmed)
            ? "placeholder_or_local_default"
            : null;
    }

    private static bool IsStrongSecret(string? value, int minLength) =>
        GetSecretProblem(value, minLength) is null;

    private static (bool Valid, string? Reason, int? KeyLengthBytes) ValidateDataProtectionKeyEncryptionKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (false, "missing", null);
        }

        if (LooksLikePlaceholder(value))
        {
            return (false, "placeholder_or_local_default", null);
        }

        try
        {
            var bytes = Convert.FromBase64String(value.Trim());
            if (LooksLikeDecodedPlaceholder(bytes))
            {
                return (false, "placeholder_or_local_default", bytes.Length);
            }

            return bytes.Length is 16 or 24 or 32
                ? (true, null, bytes.Length)
                : (false, "must_decode_to_16_24_or_32_bytes", bytes.Length);
        }
        catch (FormatException)
        {
            return (false, "must_be_base64", null);
        }
    }

    private static bool LooksLikePlaceholder(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("change_me", StringComparison.Ordinal)
            || normalized.Contains("changeme", StringComparison.Ordinal)
            || normalized.Contains("your", StringComparison.Ordinal)
            || normalized.Contains("example", StringComparison.Ordinal)
            || normalized.Contains("placeholder", StringComparison.Ordinal)
            || normalized.Contains("local-", StringComparison.Ordinal)
            || normalized.Contains("local_", StringComparison.Ordinal)
            || normalized.Contains("catalog_only_local", StringComparison.Ordinal)
            || normalized.Contains("smoke", StringComparison.Ordinal)
            || normalized.Contains("dev-secret", StringComparison.Ordinal)
            || normalized.Contains("test-secret", StringComparison.Ordinal);
    }

    private static bool LooksLikeDecodedPlaceholder(byte[] bytes)
    {
        var decoded = System.Text.Encoding.UTF8.GetString(bytes).ToLowerInvariant();
        return decoded.Contains("0123456789abcdef", StringComparison.Ordinal)
            || decoded.Contains("change_me", StringComparison.Ordinal)
            || decoded.Contains("changeme", StringComparison.Ordinal)
            || decoded.Contains("local", StringComparison.Ordinal)
            || decoded.Contains("placeholder", StringComparison.Ordinal)
            || decoded.Contains("smoke", StringComparison.Ordinal)
            || decoded.Contains("test-secret", StringComparison.Ordinal);
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration.GetConnectionString("DefaultConnection"),
            configuration["ConnectionStrings:DefaultConnection"])
        ?? string.Empty;

    private static void AddLocalTargetIfNeeded(List<object> targets, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var host = TryExtractHost(value);
        if (host is null || !IsLoopbackHost(host))
        {
            return;
        }

        targets.Add(new { name, host });
    }

    private static string? TryExtractHost(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        var segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            if (!key.Equals("Host", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return segment[(separatorIndex + 1)..].Trim();
        }

        var firstRedisSegment = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (firstRedisSegment is null)
        {
            return null;
        }

        var host = firstRedisSegment.Split(':', 2, StringSplitOptions.TrimEntries)[0];
        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static ProductionReadinessCheck Pass(string name, string message, object? details = null) => new()
    {
        Name = name,
        Status = "pass",
        Severity = "info",
        Message = message,
        Details = details
    };

    private static ProductionReadinessCheck Warn(string name, string message, object? details = null) => new()
    {
        Name = name,
        Status = "warn",
        Severity = "warning",
        Message = message,
        Details = details
    };

    private static ProductionReadinessCheck Fail(string name, string message, object? details = null) => new()
    {
        Name = name,
        Status = "fail",
        Severity = "error",
        Message = message,
        Details = details
    };
}
