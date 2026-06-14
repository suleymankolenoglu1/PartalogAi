using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Katalogcu.API.Services;

public sealed class DistributedRateLimitOptions
{
    public const string SectionName = "DistributedRateLimits";

    public bool RedisPublicChatEnabled { get; set; }
    public string RedisConnectionString { get; set; } = "";
    public string RedisKeyPrefix { get; set; } = "partalog:rate-limit";
    public int PublicChatPermitLimit { get; set; } = 20;
    public int PublicChatWindowSeconds { get; set; } = 60;
    public bool FailOpen { get; set; } = true;
    public string TooManyRequestsMessage { get; set; } = "Çok fazla istek gönderildi. Lütfen kısa süre sonra tekrar deneyin.";
}

public sealed class DistributedRateLimitResult
{
    public bool Allowed { get; init; } = true;
    public int? RetryAfterSeconds { get; init; }
    public string Reason { get; init; } = "allowed";

    public static DistributedRateLimitResult Allow(string reason = "allowed") => new()
    {
        Allowed = true,
        Reason = reason
    };
}

public interface IDistributedPublicChatRateLimiter
{
    bool Enabled { get; }
    string TooManyRequestsMessage { get; }
    Task<DistributedRateLimitResult> TryAcquireAsync(HttpContext httpContext, CancellationToken cancellationToken);
}

public sealed class RedisDistributedPublicChatRateLimiter : IDistributedPublicChatRateLimiter
{
    private const string AcquireScript =
        """
        local key = KEYS[1]
        local limit = tonumber(ARGV[1])
        local window_seconds = tonumber(ARGV[2])

        local current = redis.call('INCR', key)
        if current == 1 then
            redis.call('EXPIRE', key, window_seconds)
        end

        local ttl = redis.call('TTL', key)
        if current > limit then
            return {0, ttl}
        end

        return {1, ttl}
        """;

    private readonly DistributedRateLimitOptions _options;
    private readonly ILogger<RedisDistributedPublicChatRateLimiter> _logger;
    private readonly object _connectionLock = new();
    private Task<ConnectionMultiplexer>? _connectionTask;

    public RedisDistributedPublicChatRateLimiter(
        IOptions<DistributedRateLimitOptions> options,
        ILogger<RedisDistributedPublicChatRateLimiter> logger)
    {
        _options = Normalize(options.Value);
        _logger = logger;
    }

    public bool Enabled => _options.RedisPublicChatEnabled;

    public string TooManyRequestsMessage => string.IsNullOrWhiteSpace(_options.TooManyRequestsMessage)
        ? "Çok fazla istek gönderildi. Lütfen kısa süre sonra tekrar deneyin."
        : _options.TooManyRequestsMessage;

    public async Task<DistributedRateLimitResult> TryAcquireAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return DistributedRateLimitResult.Allow("disabled");
        }

        if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
        {
            _logger.LogWarning("Distributed public chat rate limit açık ama RedisConnectionString boş.");
            return _options.FailOpen
                ? DistributedRateLimitResult.Allow("redis_not_configured")
                : new DistributedRateLimitResult { Allowed = false, Reason = "redis_not_configured" };
        }

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var key = BuildRedisKey(httpContext);
            var result = await database.ScriptEvaluateAsync(
                AcquireScript,
                [key],
                [_options.PublicChatPermitLimit, _options.PublicChatWindowSeconds]).WaitAsync(cancellationToken);

            var values = (RedisResult[])result!;
            var allowed = (long)values[0] == 1;
            var retryAfterSeconds = Math.Max(1, (int)(long)values[1]);

            return new DistributedRateLimitResult
            {
                Allowed = allowed,
                RetryAfterSeconds = allowed ? null : retryAfterSeconds,
                Reason = allowed ? "allowed" : "redis_public_chat_limited"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed public chat rate limit Redis kontrolü başarısız.");
            return _options.FailOpen
                ? DistributedRateLimitResult.Allow("redis_unavailable")
                : new DistributedRateLimitResult { Allowed = false, Reason = "redis_unavailable" };
        }
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        Task<ConnectionMultiplexer> connectionTask;
        lock (_connectionLock)
        {
            _connectionTask ??= ConnectionMultiplexer.ConnectAsync(_options.RedisConnectionString);
            connectionTask = _connectionTask;
        }

        var connection = await connectionTask.WaitAsync(cancellationToken);
        return connection.GetDatabase();
    }

    private RedisKey BuildRedisKey(HttpContext httpContext)
    {
        var clientKey = GetClientKey(httpContext);
        var clientHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientKey))).ToLowerInvariant();
        var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / _options.PublicChatWindowSeconds;
        return $"{_options.RedisKeyPrefix.TrimEnd(':')}:public-chat:{clientHash}:{window}";
    }

    private static string GetClientKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?? "unknown-ip";
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
    }

    private static DistributedRateLimitOptions Normalize(DistributedRateLimitOptions options)
    {
        if (options.PublicChatPermitLimit <= 0)
        {
            options.PublicChatPermitLimit = 20;
        }

        if (options.PublicChatWindowSeconds <= 0)
        {
            options.PublicChatWindowSeconds = 60;
        }

        if (string.IsNullOrWhiteSpace(options.RedisKeyPrefix))
        {
            options.RedisKeyPrefix = "partalog:rate-limit";
        }

        return options;
    }
}

public sealed class DistributedPublicChatRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public DistributedPublicChatRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedPublicChatRateLimiter limiter)
    {
        if (!ShouldRateLimit(context) || context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var result = await limiter.TryAcquireAsync(context, context.RequestAborted);
        if (result.Allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json; charset=utf-8";
        if (result.RetryAfterSeconds.HasValue)
        {
            context.Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();
        }

        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = limiter.TooManyRequestsMessage,
            reason = result.Reason
        });
    }

    private static bool ShouldRateLimit(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return false;
        }

        return context.Request.Path.Equals("/api/chat/ask", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/api/chat/ask-stream", StringComparison.OrdinalIgnoreCase);
    }
}
