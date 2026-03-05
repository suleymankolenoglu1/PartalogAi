using System.Data;
using System.Data.Common;
using System.Text.Json;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed record EmbedSettingsDto(
    Guid UserId,
    IReadOnlyList<string> AllowedOrigins,
    string Theme,
    string Mode);

public interface IEmbedOriginService
{
    string NormalizeOrigin(string? origin);
    Task<bool> IsOriginAllowedAsync(Guid userId, string origin, CancellationToken cancellationToken);
    Task<EmbedSettingsDto> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken);
    Task<EmbedSettingsDto> UpsertAsync(Guid userId, IEnumerable<string> allowedOrigins, string? theme, string? mode, CancellationToken cancellationToken);
}

public sealed class EmbedOriginService : IEmbedOriginService
{
    private readonly AppDbContext _context;

    public EmbedOriginService(AppDbContext context)
    {
        _context = context;
    }

    public string NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return string.Empty;
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)) return string.Empty;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        var normalized = builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return normalized.ToLowerInvariant();
    }

    public Task<bool> IsOriginAllowedAsync(Guid userId, string origin, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();

        var normalizedOrigin = NormalizeOrigin(origin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin)) return Task.FromResult(false);

        var rawAllowedOrigins = ExecuteWithCommand(
            """
            SELECT "AllowedOrigins"
            FROM "EmbedSettings"
            WHERE "UserId" = @userId
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "userId", userId);
                var scalar = command.ExecuteScalar();
                return scalar == null || scalar is DBNull ? null : Convert.ToString(scalar);
            });

        if (string.IsNullOrWhiteSpace(rawAllowedOrigins)) return Task.FromResult(false);

        var allowedOrigins = ParseAllowedOrigins(rawAllowedOrigins);
        return Task.FromResult(allowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase));
    }

    public Task<EmbedSettingsDto> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();

        var existing = TryGetByUserId(userId);
        if (existing != null)
        {
            return Task.FromResult(existing);
        }

        var now = DateTime.UtcNow;
        ExecuteWithCommand(
            """
            INSERT INTO "EmbedSettings"
                ("Id", "UserId", "AllowedOrigins", "Theme", "Mode", "CreatedDate", "UpdatedDate")
            VALUES
                (@id, @userId, @allowedOrigins, @theme, @mode, @createdDate, NULL)
            ON CONFLICT ("UserId")
            DO NOTHING
            """,
            command =>
            {
                AddParameter(command, "id", Guid.NewGuid());
                AddParameter(command, "userId", userId);
                AddParameter(command, "allowedOrigins", "[]");
                AddParameter(command, "theme", "default");
                AddParameter(command, "mode", "catalog");
                AddParameter(command, "createdDate", now);
                command.ExecuteNonQuery();
            });

        var created = TryGetByUserId(userId) ?? new EmbedSettingsDto(userId, [], "default", "catalog");
        return Task.FromResult(created);
    }

    public Task<EmbedSettingsDto> UpsertAsync(
        Guid userId,
        IEnumerable<string> allowedOrigins,
        string? theme,
        string? mode,
        CancellationToken cancellationToken)
    {
        EnsureStorageSchema();

        var normalizedOrigins = allowedOrigins
            .Select(NormalizeOrigin)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var safeTheme = string.IsNullOrWhiteSpace(theme) ? "default" : theme.Trim().ToLowerInvariant();
        var safeMode = string.IsNullOrWhiteSpace(mode) ? "catalog" : mode.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        ExecuteWithCommand(
            """
            INSERT INTO "EmbedSettings"
                ("Id", "UserId", "AllowedOrigins", "Theme", "Mode", "CreatedDate", "UpdatedDate")
            VALUES
                (@id, @userId, @allowedOrigins, @theme, @mode, @createdDate, @updatedDate)
            ON CONFLICT ("UserId")
            DO UPDATE SET
                "AllowedOrigins" = EXCLUDED."AllowedOrigins",
                "Theme" = EXCLUDED."Theme",
                "Mode" = EXCLUDED."Mode",
                "UpdatedDate" = EXCLUDED."UpdatedDate"
            """,
            command =>
            {
                AddParameter(command, "id", Guid.NewGuid());
                AddParameter(command, "userId", userId);
                AddParameter(command, "allowedOrigins", JsonSerializer.Serialize(normalizedOrigins));
                AddParameter(command, "theme", safeTheme);
                AddParameter(command, "mode", safeMode);
                AddParameter(command, "createdDate", now);
                AddParameter(command, "updatedDate", now);
                command.ExecuteNonQuery();
            });

        var updated = TryGetByUserId(userId) ?? new EmbedSettingsDto(userId, normalizedOrigins, safeTheme, safeMode);
        return Task.FromResult(updated);
    }

    private EmbedSettingsDto? TryGetByUserId(Guid userId)
    {
        return ExecuteWithCommand(
            """
            SELECT "UserId", "AllowedOrigins", "Theme", "Mode"
            FROM "EmbedSettings"
            WHERE "UserId" = @userId
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "userId", userId);
                using var reader = command.ExecuteReader();
                if (!reader.Read()) return null;

                var rowUserId = reader.GetGuid(0);
                var allowedRaw = reader.IsDBNull(1) ? "[]" : reader.GetString(1);
                var rowTheme = reader.IsDBNull(2) ? "default" : reader.GetString(2);
                var rowMode = reader.IsDBNull(3) ? "catalog" : reader.GetString(3);

                return new EmbedSettingsDto(
                    rowUserId,
                    ParseAllowedOrigins(allowedRaw),
                    rowTheme,
                    rowMode);
            });
    }

    private static IReadOnlyList<string> ParseAllowedOrigins(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var input = raw.Trim();
        if (input.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var asJson = JsonSerializer.Deserialize<string[]>(input) ?? [];
                return asJson
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().TrimEnd('/').ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                // fallback splitting path
            }
        }

        return input
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().TrimEnd('/').ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            CREATE TABLE IF NOT EXISTS "EmbedSettings" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "AllowedOrigins" text NOT NULL DEFAULT '[]',
                "Theme" text NOT NULL DEFAULT 'default',
                "Mode" text NOT NULL DEFAULT 'catalog',
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                CONSTRAINT "PK_EmbedSettings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EmbedSettings_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmbedSettings_UserId"
            ON "EmbedSettings" ("UserId");
            """,
            command => command.ExecuteNonQuery());
    }
}
