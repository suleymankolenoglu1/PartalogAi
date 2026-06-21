using System.Data;
using System.Data.Common;
using Katalogcu.Domain.Enums;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed class AiQuotaConsumeResult
{
    public bool Allowed { get; init; }
    public string Message { get; init; } = string.Empty;
    public SubscriptionPlan Plan { get; init; }
    public int? MonthlyLimit { get; init; }
    public int UsedThisMonth { get; init; }
    public int UsedBeforeConsume { get; init; }
}

public sealed class AiUsageSnapshot
{
    public SubscriptionPlan Plan { get; init; }
    public bool AiEnabled { get; init; }
    public bool Unlimited { get; init; }
    public int? MonthlyLimit { get; init; }
    public int UsedThisMonth { get; init; }
    public int RemainingThisMonth { get; init; }
}

public interface IAiUsageQuotaService
{
    Task<AiQuotaConsumeResult> ConsumeAsync(Guid userId, CancellationToken cancellationToken);
    Task<AiUsageSnapshot> GetCurrentUsageAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class AiUsageQuotaService : IAiUsageQuotaService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AiUsageQuotaService> _logger;

    public AiUsageQuotaService(AppDbContext dbContext, ILogger<AiUsageQuotaService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AiQuotaConsumeResult> ConsumeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userPlan = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (SubscriptionPlan?)u.SubscriptionPlan)
            .FirstOrDefaultAsync(cancellationToken);

        if (userPlan is null)
        {
            return new AiQuotaConsumeResult
            {
                Allowed = false,
                Message = "Kullanıcı bulunamadı.",
                Plan = SubscriptionPlan.CatalogOnly,
                MonthlyLimit = 0
            };
        }

        var limits = PlanLimitRules.For(userPlan.Value);
        if (!limits.AiEnabled || limits.MaxAiQueriesPerMonth == 0)
        {
            return new AiQuotaConsumeResult
            {
                Allowed = false,
                Message = "AI sorgu limitinize ulaştınız, planınızı yükseltin",
                Plan = userPlan.Value,
                MonthlyLimit = 0
            };
        }

        if (limits.MaxAiQueriesPerMonth is null)
        {
            return new AiQuotaConsumeResult
            {
                Allowed = true,
                Message = string.Empty,
                Plan = userPlan.Value,
                MonthlyLimit = null
            };
        }

        var monthlyLimit = limits.MaxAiQueriesPerMonth.Value;
        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var connection = _dbContext.Database.GetDbConnection();
        var openedConnection = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            openedConnection = true;
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var inserted = await EnsureMonthlyRowAsync(connection, transaction, userId, monthStartUtc, nowUtc, cancellationToken);
            if (!inserted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AiQuotaConsumeResult
                {
                    Allowed = false,
                    Message = "AI kullanım kaydı oluşturulamadı.",
                    Plan = userPlan.Value,
                    MonthlyLimit = monthlyLimit
                };
            }

            var usedAfterConsume = await TryConsumeWithinLimitAsync(
                connection,
                transaction,
                userId,
                monthStartUtc,
                nowUtc,
                monthlyLimit,
                cancellationToken);

            if (usedAfterConsume.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                var used = usedAfterConsume.Value;
                return new AiQuotaConsumeResult
                {
                    Allowed = true,
                    Message = string.Empty,
                    Plan = userPlan.Value,
                    MonthlyLimit = monthlyLimit,
                    UsedThisMonth = used,
                    UsedBeforeConsume = Math.Max(used - 1, 0)
                };
            }

            var currentUsed = await ReadCurrentUsageAsync(connection, transaction, userId, monthStartUtc, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "AI quota blocked. userId={UserId} plan={Plan} used={Used} limit={Limit}",
                userId,
                userPlan.Value,
                currentUsed,
                monthlyLimit);

            return new AiQuotaConsumeResult
            {
                Allowed = false,
                Message = "AI sorgu limitinize ulaştınız, planınızı yükseltin",
                Plan = userPlan.Value,
                MonthlyLimit = monthlyLimit,
                UsedThisMonth = currentUsed,
                UsedBeforeConsume = currentUsed
            };
        }
        finally
        {
            if (openedConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<AiUsageSnapshot> GetCurrentUsageAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userPlan = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (SubscriptionPlan?)u.SubscriptionPlan)
            .FirstOrDefaultAsync(cancellationToken);

        if (userPlan is null)
        {
            return new AiUsageSnapshot
            {
                Plan = SubscriptionPlan.CatalogOnly,
                AiEnabled = false,
                Unlimited = false,
                MonthlyLimit = 0,
                UsedThisMonth = 0,
                RemainingThisMonth = 0
            };
        }

        var limits = PlanLimitRules.For(userPlan.Value);
        var monthlyLimit = limits.MaxAiQueriesPerMonth;
        if (!limits.AiEnabled || monthlyLimit == 0)
        {
            return new AiUsageSnapshot
            {
                Plan = userPlan.Value,
                AiEnabled = false,
                Unlimited = false,
                MonthlyLimit = 0,
                UsedThisMonth = 0,
                RemainingThisMonth = 0
            };
        }

        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var connection = _dbContext.Database.GetDbConnection();
        var openedConnection = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            openedConnection = true;
        }

        try
        {
            var usedThisMonth = await ReadCurrentUsageAsync(
                connection,
                transaction: null,
                userId,
                monthStartUtc,
                cancellationToken);

            if (monthlyLimit is null)
            {
                return new AiUsageSnapshot
                {
                    Plan = userPlan.Value,
                    AiEnabled = true,
                    Unlimited = true,
                    MonthlyLimit = null,
                    UsedThisMonth = usedThisMonth,
                    RemainingThisMonth = int.MaxValue
                };
            }

            return new AiUsageSnapshot
            {
                Plan = userPlan.Value,
                AiEnabled = true,
                Unlimited = false,
                MonthlyLimit = monthlyLimit.Value,
                UsedThisMonth = usedThisMonth,
                RemainingThisMonth = Math.Max(monthlyLimit.Value - usedThisMonth, 0)
            };
        }
        finally
        {
            if (openedConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> EnsureMonthlyRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid userId,
        DateTime monthStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO "UserAiUsageMonthly"
                ("UserId", "MonthStartUtc", "QueryCount", "CreatedDate", "UpdatedDate")
            VALUES
                (@userId, @monthStartUtc, 0, @nowUtc, @nowUtc)
            ON CONFLICT ("UserId", "MonthStartUtc") DO NOTHING;
            """;

        AddParameter(cmd, "userId", userId);
        AddParameter(cmd, "monthStartUtc", monthStartUtc);
        AddParameter(cmd, "nowUtc", nowUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task<int?> TryConsumeWithinLimitAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid userId,
        DateTime monthStartUtc,
        DateTime nowUtc,
        int monthlyLimit,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            UPDATE "UserAiUsageMonthly"
            SET "QueryCount" = "QueryCount" + 1,
                "UpdatedDate" = @nowUtc
            WHERE "UserId" = @userId
              AND "MonthStartUtc" = @monthStartUtc
              AND "QueryCount" < @limit
            RETURNING "QueryCount";
            """;

        AddParameter(cmd, "userId", userId);
        AddParameter(cmd, "monthStartUtc", monthStartUtc);
        AddParameter(cmd, "nowUtc", nowUtc);
        AddParameter(cmd, "limit", monthlyLimit);

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        if (scalar == null || scalar is DBNull) return null;
        return Convert.ToInt32(scalar);
    }

    private static async Task<int> ReadCurrentUsageAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid userId,
        DateTime monthStartUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            SELECT "QueryCount"
            FROM "UserAiUsageMonthly"
            WHERE "UserId" = @userId
              AND "MonthStartUtc" = @monthStartUtc
            LIMIT 1;
            """;

        AddParameter(cmd, "userId", userId);
        AddParameter(cmd, "monthStartUtc", monthStartUtc);
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        if (scalar == null || scalar is DBNull) return 0;
        return Convert.ToInt32(scalar);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
