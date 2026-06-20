using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class ProductionReadinessReport
{
    public string Status { get; init; } = "unknown";
    public string Environment { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ProductionReadinessCheck> Checks { get; init; } = [];
}

public sealed class ProductionReadinessCheck
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "unknown";
    public string Severity { get; init; } = "error";
    public string Message { get; init; } = "";
    public object? Details { get; init; }
}

public interface IProductionReadinessService
{
    Task<ProductionReadinessReport> CheckAsync(CancellationToken cancellationToken);
}

public sealed class ProductionReadinessService : IProductionReadinessService
{
    private readonly AppDbContext _dbContext;
    private readonly IAiCapacityGuard _aiCapacityGuard;
    private readonly IProductFeaturePolicy _featurePolicy;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly FileStorageOptions _fileStorageOptions;
    private readonly AiServiceOptions _aiServiceOptions;
    private readonly DistributedRateLimitOptions _rateLimitOptions;
    private readonly DataProtectionKeyRingOptions _dataProtectionOptions;

    public ProductionReadinessService(
        AppDbContext dbContext,
        IAiCapacityGuard aiCapacityGuard,
        IProductFeaturePolicy featurePolicy,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IOptions<FileStorageOptions> fileStorageOptions,
        IOptions<AiServiceOptions> aiServiceOptions,
        IOptions<DistributedRateLimitOptions> rateLimitOptions,
        IOptions<DataProtectionKeyRingOptions> dataProtectionOptions)
    {
        _dbContext = dbContext;
        _aiCapacityGuard = aiCapacityGuard;
        _featurePolicy = featurePolicy;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _fileStorageOptions = fileStorageOptions.Value;
        _aiServiceOptions = aiServiceOptions.Value;
        _rateLimitOptions = rateLimitOptions.Value;
        _dataProtectionOptions = dataProtectionOptions.Value;
    }

    public async Task<ProductionReadinessReport> CheckAsync(CancellationToken cancellationToken)
    {
        var checks = new List<ProductionReadinessCheck>
        {
            CheckEnvironment(),
            CheckFileStorage(),
            CheckDataProtectionKeyRing(),
            CheckDistributedPublicChatRateLimit(),
            CheckNpgsqlPoolConfiguration()
        };

        checks.Add(await CheckDatabaseAsync(cancellationToken));
        checks.Add(await CheckAiCapacityAsync(cancellationToken));
        checks.Add(await CheckAiServiceAsync(cancellationToken));

        var status = checks.Any(IsFail)
            ? "blocked"
            : checks.Any(IsWarn)
                ? "ready_with_warnings"
                : "ready";

        return new ProductionReadinessReport
        {
            Status = status,
            Environment = _environment.EnvironmentName,
            CheckedAt = DateTimeOffset.UtcNow,
            Checks = checks
        };
    }

    private ProductionReadinessCheck CheckEnvironment()
    {
        return _environment.IsProduction()
            ? Pass("environment", "ASP.NET environment production modunda.")
            : Warn(
                "environment",
                $"Ortam '{_environment.EnvironmentName}'. Prod çıkış öncesi Production config ile ayrıca doğrulanmalı.");
    }

    private async Task<ProductionReadinessCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return Fail("database", "Veritabanı bağlantısı başarısız.");
            }

            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            var hasPendingModelChanges = _dbContext.Database.HasPendingModelChanges();
            if (pendingMigrations.Length > 0 || hasPendingModelChanges)
            {
                return Fail(
                    "database_migrations",
                    "Migration/model senkronu prod için hazır değil.",
                    new
                    {
                        pendingMigrationCount = pendingMigrations.Length,
                        pendingMigrations,
                        hasPendingModelChanges
                    });
            }

            return Pass("database_migrations", "Veritabanı bağlantısı, migration ve EF model senkronu hazır.");
        }
        catch (Exception ex)
        {
            return Fail("database_migrations", "Veritabanı readiness kontrolü çalıştırılamadı.", new { error = ex.Message });
        }
    }

    private async Task<ProductionReadinessCheck> CheckAiCapacityAsync(CancellationToken cancellationToken)
    {
        var health = await _aiCapacityGuard.CheckHealthAsync(cancellationToken);
        return health.Ready
            ? Pass(
                "ai_capacity",
                $"AI capacity provider hazır: {health.Mode}.",
                new { health.Mode, health.Provider, health.LatencyMs })
            : Fail(
                "ai_capacity",
                "AI capacity dependency hazır değil.",
                new { health.Mode, health.Provider, health.Error });
    }

    private async Task<ProductionReadinessCheck> CheckAiServiceAsync(CancellationToken cancellationToken)
    {
        if (!_featurePolicy.ChatbotEnabled && !_featurePolicy.CatalogAnalysisEnabled)
        {
            return Pass("ai_service", "AI modülleri kapalı; Python AI servis zorunlu değil.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PartalogAi");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await client.GetAsync("/health/ready", timeoutCts.Token);
            if (response.IsSuccessStatusCode)
            {
                return Pass("ai_service", "Python AI servis readiness başarılı.", new { _aiServiceOptions.BaseUrl });
            }

            return Fail(
                "ai_service",
                $"Python AI servis readiness başarısız: {(int)response.StatusCode}.",
                new { _aiServiceOptions.BaseUrl, statusCode = (int)response.StatusCode });
        }
        catch (Exception ex)
        {
            return Fail(
                "ai_service",
                "Python AI servis readiness kontrolü başarısız.",
                new { _aiServiceOptions.BaseUrl, error = ex.Message });
        }
    }

    private ProductionReadinessCheck CheckFileStorage()
    {
        var provider = (_fileStorageOptions.Provider ?? "Local").Trim();
        if (provider.Equals("googlecloudstorage", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("gcs", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(_fileStorageOptions.BucketName)
                ? Fail("file_storage", "GCS seçili ama FileStorage:BucketName boş.")
                : Pass("file_storage", "Dosya depolama GCS provider ile hazır.", new { provider, bucketName = _fileStorageOptions.BucketName });
        }

        if (_environment.IsProduction())
        {
            return Warn(
                "file_storage",
                "Production ortamda Local file storage kullanımı multi-instance deploy için risklidir.",
                new { provider });
        }

        return Pass("file_storage", "Dosya depolama local/dev kullanım için hazır.", new { provider });
    }

    private ProductionReadinessCheck CheckDataProtectionKeyRing()
    {
        var provider = FirstNonEmpty(_dataProtectionOptions.Provider);
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = !string.IsNullOrWhiteSpace(_dataProtectionOptions.RedisConnectionString)
                ? "Redis"
                : !string.IsNullOrWhiteSpace(_dataProtectionOptions.KeysDirectory)
                    ? "FileSystem"
                    : "";
        }

        if (provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(_dataProtectionOptions.RedisConnectionString)
                ? Fail("data_protection_key_ring", "DataProtection Redis provider seçili ama RedisConnectionString boş.")
                : BuildDataProtectionKeyRingResult(
                    provider,
                    "DataProtection key-ring Redis üzerinden shared çalışacak.",
                    new { provider, redisKey = _dataProtectionOptions.RedisKey });
        }

        if (provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_dataProtectionOptions.KeysDirectory))
            {
                return Fail("data_protection_key_ring", "DataProtection FileSystem provider seçili ama KeysDirectory boş.");
            }

            var fileSystemResult = _environment.IsProduction()
                ? Warn(
                    "data_protection_key_ring",
                    "DataProtection FileSystem provider kullanıyor. Multi-instance prod için dizinin shared ve kalıcı mount olduğundan emin olun.",
                    new { provider, keysDirectory = _dataProtectionOptions.KeysDirectory })
                : Pass(
                    "data_protection_key_ring",
                    "DataProtection key-ring local/dev için dosya sisteminde kalıcı.",
                    new { provider, keysDirectory = _dataProtectionOptions.KeysDirectory });

            return !HasDataProtectionKeyEncryption() && _environment.IsProduction()
                ? Fail("data_protection_key_ring", "Production DataProtection key-ring var ama KeyEncryptionKey boş.")
                : fileSystemResult;
        }

        return _environment.IsProduction()
            ? Fail("data_protection_key_ring", "Production ortamında DataProtection provider yapılandırılmamış.")
            : Pass("data_protection_key_ring", "DataProtection key-ring local/dev ortamda framework default ile çalışabilir.");
    }

    private ProductionReadinessCheck BuildDataProtectionKeyRingResult(
        string provider,
        string message,
        object details)
    {
        if (HasDataProtectionKeyEncryption())
        {
            return Pass(
                "data_protection_key_ring",
                $"{message} XML key encryption aktif.",
                details);
        }

        return _environment.IsProduction()
            ? Fail("data_protection_key_ring", "Production DataProtection key-ring var ama KeyEncryptionKey boş.", details)
            : Warn("data_protection_key_ring", $"{message} XML key encryption local/dev ortamda kapalı.", details);
    }

    private bool HasDataProtectionKeyEncryption() =>
        !string.IsNullOrWhiteSpace(_dataProtectionOptions.KeyEncryptionKey);

    private ProductionReadinessCheck CheckDistributedPublicChatRateLimit()
    {
        if (!_featurePolicy.ChatbotEnabled)
        {
            return Pass("distributed_public_chat_rate_limit", "Chatbot kapalı; public chat rate limit zorunlu değil.");
        }

        if (_rateLimitOptions.RedisPublicChatEnabled)
        {
            return string.IsNullOrWhiteSpace(_rateLimitOptions.RedisConnectionString)
                ? Fail("distributed_public_chat_rate_limit", "Redis public chat rate limit açık ama RedisConnectionString boş.")
                : Pass("distributed_public_chat_rate_limit", "Redis public chat rate limit aktif.");
        }

        return _environment.IsProduction()
            ? Warn("distributed_public_chat_rate_limit", "Production chatbot açık ama Redis distributed public chat rate limit kapalı.")
            : Pass("distributed_public_chat_rate_limit", "Redis public chat rate limit local/dev ortamda opsiyonel.");
    }

    private ProductionReadinessCheck CheckNpgsqlPoolConfiguration()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Fail("npgsql_pool_config", "ConnectionStrings:DefaultConnection boş.");
        }

        var requiredFragments = new[]
        {
            "Pooling=true",
            "Maximum Pool Size=",
            "Timeout=",
            "Command Timeout=",
            "Max Auto Prepare=0"
        };

        var missing = requiredFragments
            .Where(fragment => !connectionString.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0)
        {
            return _environment.IsProduction()
                ? Fail("npgsql_pool_config", "Npgsql connection string prod için pool/timeout disiplinini tam taşımıyor.", new { missing })
                : Warn("npgsql_pool_config", "Npgsql connection string pool/timeout ayarlarının bir kısmını taşımıyor.", new { missing });
        }

        var throughPgBouncer = connectionString.Contains("Port=6432", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Host=pgbouncer", StringComparison.OrdinalIgnoreCase);

        return Pass(
            "npgsql_pool_config",
            throughPgBouncer
                ? "Npgsql pool/timeout ayarları explicit ve PgBouncer endpoint'i kullanılıyor."
                : "Npgsql pool/timeout ayarları explicit; PgBouncer kullanımı connection string'de görünmüyor.",
            new { throughPgBouncer });
    }

    private static bool IsFail(ProductionReadinessCheck check) =>
        check.Status.Equals("fail", StringComparison.OrdinalIgnoreCase) && check.Severity.Equals("error", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarn(ProductionReadinessCheck check) =>
        check.Status.Equals("warn", StringComparison.OrdinalIgnoreCase);

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
