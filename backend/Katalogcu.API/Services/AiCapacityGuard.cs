using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Katalogcu.API.Services;

public sealed class AiCapacityOptions
{
    public int GlobalConcurrentChats { get; set; } = 100;
    public int PerUserConcurrentChats { get; set; } = 3;
    public int AcquireTimeoutMs { get; set; } = 150;
    public string Provider { get; set; } = "InMemory";
    public bool UseDistributedLeases { get; set; }
    public int DistributedLeaseTtlSeconds { get; set; } = 180;
    public string DistributedPoolName { get; set; } = "api-chat";
    public string RedisConnectionString { get; set; } = "";
    public string RedisKeyPrefix { get; set; } = "partalog:ai-capacity";
    public string BusyMessage { get; set; } = "AI kapasitesi şu an dolu. Lütfen birkaç saniye sonra tekrar deneyin.";
}

public sealed class AiCapacitySnapshot
{
    public int GlobalActiveChats { get; init; }
    public int GlobalConcurrentChats { get; init; }
    public int PerUserConcurrentChats { get; init; }
    public string Mode { get; init; } = "in-memory";
    public bool Distributed { get; init; }
}

public sealed class AiCapacityHealthStatus
{
    public bool Ready { get; init; }
    public string Mode { get; init; } = "in-memory";
    public string Provider { get; init; } = "InMemory";
    public double? LatencyMs { get; init; }
    public string? Error { get; init; }
}

public interface IAiCapacityGuard
{
    Task<AiCapacityLease?> TryAcquireAsync(Guid userId, string? publicToken, CancellationToken cancellationToken);
    AiCapacitySnapshot GetSnapshot();
    Task<AiCapacitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
    Task<AiCapacityHealthStatus> CheckHealthAsync(CancellationToken cancellationToken);
    string BusyMessage { get; }
}

public sealed class AiCapacityLease : IAsyncDisposable
{
    private readonly Func<ValueTask> _release;
    private int _released;

    public AiCapacityLease(Action release)
        : this(() =>
        {
            release();
            return ValueTask.CompletedTask;
        })
    {
    }

    public AiCapacityLease(Func<ValueTask> release)
    {
        _release = release;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            await _release();
        }
    }
}

public sealed class AiCapacityGuard : IAiCapacityGuard
{
    private const string DistributedLockKey = "partalog_ai_capacity";
    private const string RedisAcquireScript =
        """
        local global_key = KEYS[1]
        local partition_key = KEYS[2]
        local now_ms = tonumber(ARGV[1])
        local expires_ms = tonumber(ARGV[2])
        local global_limit = tonumber(ARGV[3])
        local partition_limit = tonumber(ARGV[4])
        local lease_id = ARGV[5]
        local ttl_ms = tonumber(ARGV[6])

        redis.call('ZREMRANGEBYSCORE', global_key, '-inf', now_ms)
        redis.call('ZREMRANGEBYSCORE', partition_key, '-inf', now_ms)

        local global_count = redis.call('ZCARD', global_key)
        if global_count >= global_limit then
            return {0, global_count}
        end

        local partition_count = redis.call('ZCARD', partition_key)
        if partition_count >= partition_limit then
            return {0, global_count}
        end

        redis.call('ZADD', global_key, expires_ms, lease_id)
        redis.call('ZADD', partition_key, expires_ms, lease_id)
        redis.call('PEXPIRE', global_key, ttl_ms * 2)
        redis.call('PEXPIRE', partition_key, ttl_ms * 2)

        return {1, global_count + 1}
        """;
    private const string RedisReleaseScript =
        """
        redis.call('ZREM', KEYS[1], ARGV[1])
        redis.call('ZREM', KEYS[2], ARGV[1])
        return 1
        """;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _partitionSemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _partitionActiveCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly AiCapacityOptions _options;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<AiCapacityGuard>? _logger;
    private readonly string _instanceId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    private readonly object _redisConnectionLock = new();
    private Task<ConnectionMultiplexer>? _redisConnectionTask;
    private int _globalActiveCount;
    private int _lastDistributedActiveCount;

    public AiCapacityGuard(IOptions<AiCapacityOptions> options)
        : this(options, null, null)
    {
    }

    public AiCapacityGuard(
        IOptions<AiCapacityOptions> options,
        IServiceScopeFactory? scopeFactory,
        ILogger<AiCapacityGuard>? logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        if (_options.GlobalConcurrentChats <= 0)
        {
            _options.GlobalConcurrentChats = 100;
        }

        if (_options.PerUserConcurrentChats <= 0)
        {
            _options.PerUserConcurrentChats = 3;
        }

        if (_options.AcquireTimeoutMs < 0)
        {
            _options.AcquireTimeoutMs = 0;
        }

        if (_options.DistributedLeaseTtlSeconds < 30)
        {
            _options.DistributedLeaseTtlSeconds = 30;
        }

        if (string.IsNullOrWhiteSpace(_options.DistributedPoolName))
        {
            _options.DistributedPoolName = "api-chat";
        }

        if (string.IsNullOrWhiteSpace(_options.RedisKeyPrefix))
        {
            _options.RedisKeyPrefix = "partalog:ai-capacity";
        }

        _globalSemaphore = new SemaphoreSlim(_options.GlobalConcurrentChats, _options.GlobalConcurrentChats);
    }

    public string BusyMessage => string.IsNullOrWhiteSpace(_options.BusyMessage)
        ? "AI kapasitesi şu an dolu. Lütfen birkaç saniye sonra tekrar deneyin."
        : _options.BusyMessage;

    public async Task<AiCapacityLease?> TryAcquireAsync(Guid userId, string? publicToken, CancellationToken cancellationToken)
    {
        var partitionKey = BuildPartitionKey(userId, publicToken);
        if (UseRedisProvider)
        {
            return await TryAcquireRedisAsync(partitionKey, cancellationToken);
        }

        if (UsePostgresProvider && _scopeFactory is not null)
        {
            return await TryAcquireDistributedAsync(partitionKey, cancellationToken);
        }

        var timeout = TimeSpan.FromMilliseconds(_options.AcquireTimeoutMs);
        if (!await _globalSemaphore.WaitAsync(timeout, cancellationToken))
        {
            return null;
        }

        var globalAcquired = true;
        var partitionSemaphore = _partitionSemaphores.GetOrAdd(
            partitionKey,
            _ => new SemaphoreSlim(_options.PerUserConcurrentChats, _options.PerUserConcurrentChats));

        try
        {
            if (!await partitionSemaphore.WaitAsync(timeout, cancellationToken))
            {
                return null;
            }

            globalAcquired = false;
            Interlocked.Increment(ref _globalActiveCount);
            _partitionActiveCounts.AddOrUpdate(partitionKey, 1, (_, current) => current + 1);

            return new AiCapacityLease(() =>
            {
                partitionSemaphore.Release();
                _globalSemaphore.Release();
                Interlocked.Decrement(ref _globalActiveCount);
                _partitionActiveCounts.AddOrUpdate(
                    partitionKey,
                    0,
                    (_, current) => Math.Max(0, current - 1));
            });
        }
        finally
        {
            if (globalAcquired)
            {
                _globalSemaphore.Release();
            }
        }
    }

    public AiCapacitySnapshot GetSnapshot()
    {
        return new AiCapacitySnapshot
        {
            GlobalActiveChats = Math.Max(0, Volatile.Read(ref _globalActiveCount)),
            GlobalConcurrentChats = _options.GlobalConcurrentChats,
            PerUserConcurrentChats = _options.PerUserConcurrentChats,
            Mode = CapacityMode,
            Distributed = UseRedisProvider || UsePostgresProvider
        };
    }

    public async Task<AiCapacitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (UseRedisProvider)
        {
            return await GetRedisSnapshotAsync(cancellationToken);
        }

        if (!UsePostgresProvider || _scopeFactory is null)
        {
            return GetSnapshot();
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection();

            await ExecuteNonQueryAsync(
                connection,
                null,
                """DELETE FROM "AiCapacityLeases" WHERE "PoolName" = @poolName AND "ExpiresAt" <= now();""",
                [("@poolName", _options.DistributedPoolName)],
                cancellationToken);

            var activeCount = await ExecuteScalarLongAsync(
                connection,
                null,
                """SELECT count(*) FROM "AiCapacityLeases" WHERE "PoolName" = @poolName AND "ExpiresAt" > now();""",
                [("@poolName", _options.DistributedPoolName)],
                cancellationToken);

            Volatile.Write(ref _lastDistributedActiveCount, (int)Math.Min(int.MaxValue, activeCount));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Dağıtık AI kapasite snapshot okunamadı; son bilinen değer döndürülecek.");
        }

        return new AiCapacitySnapshot
        {
            GlobalActiveChats = Math.Max(0, Volatile.Read(ref _lastDistributedActiveCount)),
            GlobalConcurrentChats = _options.GlobalConcurrentChats,
            PerUserConcurrentChats = _options.PerUserConcurrentChats,
            Mode = CapacityMode,
            Distributed = true
        };
    }

    public async Task<AiCapacityHealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            if (UseRedisProvider)
            {
                var database = await GetRedisDatabaseAsync(cancellationToken);
                await database.PingAsync().WaitAsync(cancellationToken);
                return BuildHealthStatus(true, startedAt);
            }

            if (UsePostgresProvider)
            {
                if (_scopeFactory is null)
                {
                    return BuildHealthStatus(false, startedAt, "Postgres capacity provider requires IServiceScopeFactory.");
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
                await ExecuteScalarLongAsync(
                    dbContext.Database.GetDbConnection(),
                    null,
                    "SELECT 1;",
                    [],
                    cancellationToken);
            }

            return BuildHealthStatus(true, startedAt);
        }
        catch (Exception ex)
        {
            return BuildHealthStatus(false, startedAt, ex.Message);
        }
    }

    private async Task<AiCapacityLease?> TryAcquireRedisAsync(string partitionKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
        {
            _logger?.LogError("AI kapasite provider Redis seçildi ama RedisConnectionString boş.");
            return null;
        }

        var leaseId = Guid.NewGuid().ToString("N");
        var (globalKey, partitionRedisKey) = BuildRedisKeys(partitionKey);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ttlMs = _options.DistributedLeaseTtlSeconds * 1000L;
        var expiresMs = nowMs + ttlMs;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(50, _options.AcquireTimeoutMs)));

        try
        {
            var database = await GetRedisDatabaseAsync(timeoutCts.Token);
            var result = await database.ScriptEvaluateAsync(
                RedisAcquireScript,
                [globalKey, partitionRedisKey],
                [
                    nowMs,
                    expiresMs,
                    _options.GlobalConcurrentChats,
                    _options.PerUserConcurrentChats,
                    leaseId,
                    ttlMs
                ]).WaitAsync(timeoutCts.Token);

            var values = (RedisResult[])result!;
            var acquired = (long)values[0] == 1;
            var activeCount = (int)Math.Min(int.MaxValue, (long)values[1]);
            Volatile.Write(ref _lastDistributedActiveCount, activeCount);

            if (!acquired)
            {
                return null;
            }

            return new AiCapacityLease(() => new ValueTask(ReleaseRedisLeaseAsync(globalKey, partitionRedisKey, leaseId)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning("Redis AI kapasite lease zaman aşımına uğradı.");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis AI kapasite lease alınamadı; istek güvenli şekilde reddedilecek.");
            return null;
        }
    }

    private async Task ReleaseRedisLeaseAsync(RedisKey globalKey, RedisKey partitionKey, string leaseId)
    {
        try
        {
            var database = await GetRedisDatabaseAsync(CancellationToken.None);
            await database.ScriptEvaluateAsync(
                RedisReleaseScript,
                [globalKey, partitionKey],
                [leaseId]);

            var current = Volatile.Read(ref _lastDistributedActiveCount);
            Volatile.Write(ref _lastDistributedActiveCount, Math.Max(0, current - 1));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis AI kapasite lease bırakılamadı; TTL temizliği devreye girecek.");
        }
    }

    private async Task<AiCapacitySnapshot> GetRedisSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = await GetRedisDatabaseAsync(cancellationToken);
            var globalKey = BuildRedisGlobalKey();
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await database.SortedSetRemoveRangeByScoreAsync(globalKey, double.NegativeInfinity, nowMs).WaitAsync(cancellationToken);
            var activeCount = await database.SortedSetLengthAsync(globalKey).WaitAsync(cancellationToken);
            Volatile.Write(ref _lastDistributedActiveCount, (int)Math.Min(int.MaxValue, activeCount));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis AI kapasite snapshot okunamadı; son bilinen değer döndürülecek.");
        }

        return new AiCapacitySnapshot
        {
            GlobalActiveChats = Math.Max(0, Volatile.Read(ref _lastDistributedActiveCount)),
            GlobalConcurrentChats = _options.GlobalConcurrentChats,
            PerUserConcurrentChats = _options.PerUserConcurrentChats,
            Mode = CapacityMode,
            Distributed = true
        };
    }

    private async Task<AiCapacityLease?> TryAcquireDistributedAsync(string partitionKey, CancellationToken cancellationToken)
    {
        var timeoutMs = Math.Max(50, _options.AcquireTimeoutMs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        try
        {
            await using var scope = _scopeFactory!.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.OpenConnectionAsync(timeoutCts.Token);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(timeoutCts.Token);
            var dbTransaction = transaction.GetDbTransaction();
            var connection = dbContext.Database.GetDbConnection();

            await ExecuteNonQueryAsync(
                connection,
                dbTransaction,
                """SELECT pg_advisory_xact_lock(hashtext(@lockKey));""",
                [("@lockKey", $"{DistributedLockKey}:{_options.DistributedPoolName}")],
                timeoutCts.Token);

            await ExecuteNonQueryAsync(
                connection,
                dbTransaction,
                """DELETE FROM "AiCapacityLeases" WHERE "PoolName" = @poolName AND "ExpiresAt" <= now();""",
                [("@poolName", _options.DistributedPoolName)],
                timeoutCts.Token);

            await ExecuteNonQueryAsync(
                connection,
                dbTransaction,
                """DELETE FROM "AiCapacityLeases" WHERE "ExpiresAt" <= now();""",
                [],
                timeoutCts.Token);

            var globalActiveCount = await ExecuteScalarLongAsync(
                connection,
                dbTransaction,
                """SELECT count(*) FROM "AiCapacityLeases" WHERE "PoolName" = @poolName AND "ExpiresAt" > now();""",
                [("@poolName", _options.DistributedPoolName)],
                timeoutCts.Token);

            if (globalActiveCount >= _options.GlobalConcurrentChats)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Volatile.Write(ref _lastDistributedActiveCount, (int)Math.Min(int.MaxValue, globalActiveCount));
                return null;
            }

            var partitionActiveCount = await ExecuteScalarLongAsync(
                connection,
                dbTransaction,
                """
                SELECT count(*)
                FROM "AiCapacityLeases"
                WHERE "PoolName" = @poolName AND "PartitionKey" = @partitionKey AND "ExpiresAt" > now();
                """,
                [("@poolName", _options.DistributedPoolName), ("@partitionKey", partitionKey)],
                timeoutCts.Token);

            if (partitionActiveCount >= _options.PerUserConcurrentChats)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Volatile.Write(ref _lastDistributedActiveCount, (int)Math.Min(int.MaxValue, globalActiveCount));
                return null;
            }

            var leaseId = Guid.NewGuid();
            await ExecuteNonQueryAsync(
                connection,
                dbTransaction,
                """
                INSERT INTO "AiCapacityLeases" ("Id", "PoolName", "PartitionKey", "InstanceId", "CreatedAt", "ExpiresAt")
                VALUES (@id, @poolName, @partitionKey, @instanceId, now(), now() + (@ttlSeconds * interval '1 second'));
                """,
                [
                    ("@id", leaseId),
                    ("@poolName", _options.DistributedPoolName),
                    ("@partitionKey", partitionKey),
                    ("@instanceId", _instanceId),
                    ("@ttlSeconds", _options.DistributedLeaseTtlSeconds)
                ],
                timeoutCts.Token);

            await transaction.CommitAsync(timeoutCts.Token);
            Volatile.Write(ref _lastDistributedActiveCount, (int)Math.Min(int.MaxValue, globalActiveCount + 1));

            return new AiCapacityLease(() => new ValueTask(ReleaseDistributedLeaseAsync(leaseId)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning("Dağıtık AI kapasite lease zaman aşımına uğradı.");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Dağıtık AI kapasite lease alınamadı; istek güvenli şekilde reddedilecek.");
            return null;
        }
    }

    private async Task ReleaseDistributedLeaseAsync(Guid leaseId)
    {
        try
        {
            await using var scope = _scopeFactory!.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.OpenConnectionAsync(CancellationToken.None);
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                null,
                """DELETE FROM "AiCapacityLeases" WHERE "PoolName" = @poolName AND "Id" = @id;""",
                [("@poolName", _options.DistributedPoolName), ("@id", leaseId)],
                CancellationToken.None);

            var current = Volatile.Read(ref _lastDistributedActiveCount);
            Volatile.Write(ref _lastDistributedActiveCount, Math.Max(0, current - 1));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Dağıtık AI kapasite lease bırakılamadı; TTL temizliği devreye girecek.");
        }
    }

    private bool UseRedisProvider =>
        string.Equals(NormalizedProvider, "redis", StringComparison.OrdinalIgnoreCase);

    private bool UsePostgresProvider =>
        string.Equals(NormalizedProvider, "postgres", StringComparison.OrdinalIgnoreCase)
        || string.Equals(NormalizedProvider, "postgres-distributed", StringComparison.OrdinalIgnoreCase)
        || (_options.UseDistributedLeases && string.Equals(NormalizedProvider, "inmemory", StringComparison.OrdinalIgnoreCase));

    private string CapacityMode => UseRedisProvider
        ? "redis-distributed"
        : UsePostgresProvider
            ? "postgres-distributed"
            : "in-memory";

    private AiCapacityHealthStatus BuildHealthStatus(bool ready, DateTimeOffset startedAt, string? error = null)
    {
        return new AiCapacityHealthStatus
        {
            Ready = ready,
            Mode = CapacityMode,
            Provider = _options.Provider,
            LatencyMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            Error = error
        };
    }

    private string NormalizedProvider
    {
        get
        {
            var provider = (_options.Provider ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(provider))
            {
                return _options.UseDistributedLeases ? "postgres" : "inmemory";
            }

            provider = provider.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            return provider.Equals("inmemory", StringComparison.OrdinalIgnoreCase)
                ? "inmemory"
                : provider.ToLowerInvariant();
        }
    }

    private async Task<StackExchange.Redis.IDatabase> GetRedisDatabaseAsync(CancellationToken cancellationToken)
    {
        Task<ConnectionMultiplexer> connectionTask;
        lock (_redisConnectionLock)
        {
            _redisConnectionTask ??= ConnectionMultiplexer.ConnectAsync(_options.RedisConnectionString);
            connectionTask = _redisConnectionTask;
        }

        var connection = await connectionTask.WaitAsync(cancellationToken);
        return connection.GetDatabase();
    }

    private RedisKey BuildRedisGlobalKey()
    {
        return $"{_options.RedisKeyPrefix.TrimEnd(':')}:{_options.DistributedPoolName}:global";
    }

    private (RedisKey GlobalKey, RedisKey PartitionKey) BuildRedisKeys(string partitionKey)
    {
        var globalKey = BuildRedisGlobalKey();
        var partitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey))).ToLowerInvariant();
        var partitionRedisKey = $"{_options.RedisKeyPrefix.TrimEnd(':')}:{_options.DistributedPoolName}:partition:{partitionHash}";
        return (globalKey, partitionRedisKey);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string commandText,
        IReadOnlyCollection<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, commandText, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ExecuteScalarLongAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string commandText,
        IReadOnlyCollection<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, commandText, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? transaction,
        string commandText,
        IReadOnlyCollection<(string Name, object Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string BuildPartitionKey(Guid userId, string? publicToken)
    {
        if (userId != Guid.Empty)
        {
            return $"user:{userId:N}";
        }

        return string.IsNullOrWhiteSpace(publicToken)
            ? "anonymous"
            : $"public:{publicToken.Trim()}";
    }
}
