using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed record EmbedAnalyticsSummary(
    int TotalEvents,
    int EventsLast7Days,
    int PartViewedCount,
    int CartAddCount,
    int CheckoutStartCount);

public interface IEmbedAnalyticsService
{
    Task IngestAsync(
        Guid ownerUserId,
        string eventName,
        string source,
        string fingerprintHash,
        string? origin,
        string? pageUrl,
        string? payloadJson,
        CancellationToken cancellationToken);

    Task<EmbedAnalyticsSummary> GetSummaryAsync(Guid ownerUserId, CancellationToken cancellationToken);
    string BuildFingerprint(string? ip, string? userAgent, string? acceptLanguage);
}

public sealed class EmbedAnalyticsService : IEmbedAnalyticsService
{
    private readonly AppDbContext _context;

    public EmbedAnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public Task IngestAsync(
        Guid ownerUserId,
        string eventName,
        string source,
        string fingerprintHash,
        string? origin,
        string? pageUrl,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        EnsureStorageSchema();

        ExecuteWithCommand(
            """
            INSERT INTO "EmbedEvents"
                ("Id", "OwnerUserId", "EventName", "Source", "FingerprintHash", "Origin", "PageUrl", "PayloadJson", "CreatedDate")
            VALUES
                (@id, @ownerUserId, @eventName, @source, @fingerprintHash, @origin, @pageUrl, @payloadJson, @createdDate)
            """,
            command =>
            {
                AddParameter(command, "id", Guid.NewGuid());
                AddParameter(command, "ownerUserId", ownerUserId);
                AddParameter(command, "eventName", eventName.Trim().ToLowerInvariant());
                AddParameter(command, "source", source.Trim().ToLowerInvariant());
                AddParameter(command, "fingerprintHash", fingerprintHash.Trim());
                AddParameter(command, "origin", (object?)origin ?? DBNull.Value);
                AddParameter(command, "pageUrl", (object?)pageUrl ?? DBNull.Value);
                AddParameter(command, "payloadJson", (object?)payloadJson ?? DBNull.Value);
                AddParameter(command, "createdDate", DateTime.UtcNow);
                command.ExecuteNonQuery();
            });

        return Task.CompletedTask;
    }

    public Task<EmbedAnalyticsSummary> GetSummaryAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();
        var since7Days = DateTime.UtcNow.AddDays(-7);

        var total = ExecuteScalarInt(
            """
            SELECT COUNT(*)
            FROM "EmbedEvents"
            WHERE "OwnerUserId" = @ownerUserId
            """,
            ownerUserId);

        var last7Days = ExecuteScalarInt(
            """
            SELECT COUNT(*)
            FROM "EmbedEvents"
            WHERE "OwnerUserId" = @ownerUserId
              AND "CreatedDate" >= @fromUtc
            """,
            ownerUserId,
            since7Days);

        var partViewed = ExecuteScalarInt(
            """
            SELECT COUNT(*)
            FROM "EmbedEvents"
            WHERE "OwnerUserId" = @ownerUserId
              AND "EventName" = 'part:viewed'
            """,
            ownerUserId);

        var cartAdd = ExecuteScalarInt(
            """
            SELECT COUNT(*)
            FROM "EmbedEvents"
            WHERE "OwnerUserId" = @ownerUserId
              AND "EventName" = 'cart:add'
            """,
            ownerUserId);

        var checkoutStart = ExecuteScalarInt(
            """
            SELECT COUNT(*)
            FROM "EmbedEvents"
            WHERE "OwnerUserId" = @ownerUserId
              AND "EventName" = 'checkout:start'
            """,
            ownerUserId);

        return Task.FromResult(new EmbedAnalyticsSummary(
            total,
            last7Days,
            partViewed,
            cartAdd,
            checkoutStart));
    }

    public string BuildFingerprint(string? ip, string? userAgent, string? acceptLanguage)
    {
        var raw = $"{ip ?? "unknown"}|{userAgent ?? "unknown"}|{acceptLanguage ?? "unknown"}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private int ExecuteScalarInt(string sql, Guid ownerUserId, DateTime? fromUtc = null)
    {
        return ExecuteWithCommand(
            sql,
            command =>
            {
                AddParameter(command, "ownerUserId", ownerUserId);
                if (fromUtc.HasValue)
                {
                    AddParameter(command, "fromUtc", fromUtc.Value);
                }

                var scalar = command.ExecuteScalar();
                return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
            });
    }

    private T ExecuteWithCommand<T>(string sql, Func<DbCommand, T> action)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return action(command);
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                connection.Close();
            }
        }
    }

    private void ExecuteWithCommand(string sql, Action<DbCommand> action)
    {
        ExecuteWithCommand<object?>(
            sql,
            command =>
            {
                action(command);
                return null;
            });
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = $"@{name}";
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private void EnsureStorageSchema()
    {
        ExecuteWithCommand(
            """
            CREATE TABLE IF NOT EXISTS "EmbedEvents" (
                "Id" uuid NOT NULL,
                "OwnerUserId" uuid NOT NULL,
                "EventName" text NOT NULL,
                "Source" text NOT NULL,
                "FingerprintHash" text NOT NULL,
                "Origin" text NULL,
                "PageUrl" text NULL,
                "PayloadJson" text NULL,
                "CreatedDate" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_EmbedEvents" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EmbedEvents_Users_OwnerUserId"
                    FOREIGN KEY ("OwnerUserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE INDEX IF NOT EXISTS "IX_EmbedEvents_OwnerUserId_CreatedDate"
            ON "EmbedEvents" ("OwnerUserId", "CreatedDate");
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE INDEX IF NOT EXISTS "IX_EmbedEvents_OwnerUserId_EventName_CreatedDate"
            ON "EmbedEvents" ("OwnerUserId", "EventName", "CreatedDate");
            """,
            command => command.ExecuteNonQuery());
    }
}
