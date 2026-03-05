using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed record EmbedDomainVerificationDto(
    Guid Id,
    Guid UserId,
    string Origin,
    string Domain,
    string Method,
    string ChallengeToken,
    string Status,
    string? LastError,
    DateTime CreatedDate,
    DateTime? UpdatedDate,
    DateTime? VerifiedAt);

public interface IEmbedDomainVerificationService
{
    Task<IReadOnlyList<EmbedDomainVerificationDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<EmbedDomainVerificationDto> CreateChallengeAsync(Guid userId, string origin, string method, CancellationToken cancellationToken);
    Task<EmbedDomainVerificationDto?> VerifyNowAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken);
    Task<EmbedDomainVerificationDto?> ActivateOriginAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken);
}

public sealed class EmbedDomainVerificationService : IEmbedDomainVerificationService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbedOriginService _embedOriginService;

    public EmbedDomainVerificationService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IEmbedOriginService embedOriginService)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _embedOriginService = embedOriginService;
    }

    public Task<IReadOnlyList<EmbedDomainVerificationDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();
        var list = ExecuteWithCommand(
            """
            SELECT "Id", "UserId", "Origin", "Domain", "Method", "ChallengeToken", "Status", "LastError", "CreatedDate", "UpdatedDate", "VerifiedAt"
            FROM "EmbedDomainVerifications"
            WHERE "UserId" = @userId
            ORDER BY "CreatedDate" DESC
            """,
            command =>
            {
                AddParameter(command, "userId", userId);
                using var reader = command.ExecuteReader();
                var results = new List<EmbedDomainVerificationDto>();
                while (reader.Read())
                {
                    results.Add(MapReader(reader));
                }
                return (IReadOnlyList<EmbedDomainVerificationDto>)results;
            });
        return Task.FromResult(list);
    }

    public Task<EmbedDomainVerificationDto> CreateChallengeAsync(Guid userId, string origin, string method, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();
        var normalizedOrigin = _embedOriginService.NormalizeOrigin(origin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            throw new InvalidOperationException("Geçerli bir origin girin.");
        }

        var parsed = new Uri(normalizedOrigin);
        var domain = parsed.Host.ToLowerInvariant();
        var safeMethod = NormalizeMethod(method);
        if (safeMethod == null)
        {
            throw new InvalidOperationException("Doğrulama metodu geçersiz.");
        }

        var token = GenerateChallengeToken();
        var now = DateTime.UtcNow;

        ExecuteWithCommand(
            """
            INSERT INTO "EmbedDomainVerifications"
                ("Id", "UserId", "Origin", "Domain", "Method", "ChallengeToken", "Status", "LastError", "CreatedDate", "UpdatedDate", "VerifiedAt")
            VALUES
                (@id, @userId, @origin, @domain, @method, @challengeToken, 'pending', NULL, @createdDate, NULL, NULL)
            ON CONFLICT ("UserId", "Origin")
            DO UPDATE SET
                "Domain" = EXCLUDED."Domain",
                "Method" = EXCLUDED."Method",
                "ChallengeToken" = EXCLUDED."ChallengeToken",
                "Status" = 'pending',
                "LastError" = NULL,
                "UpdatedDate" = @updatedDate,
                "VerifiedAt" = NULL
            """,
            command =>
            {
                AddParameter(command, "id", Guid.NewGuid());
                AddParameter(command, "userId", userId);
                AddParameter(command, "origin", normalizedOrigin);
                AddParameter(command, "domain", domain);
                AddParameter(command, "method", safeMethod);
                AddParameter(command, "challengeToken", token);
                AddParameter(command, "createdDate", now);
                AddParameter(command, "updatedDate", now);
                command.ExecuteNonQuery();
            });

        var row = GetByOrigin(userId, normalizedOrigin);
        if (row == null) throw new InvalidOperationException("Doğrulama kaydı oluşturulamadı.");
        return Task.FromResult(row);
    }

    public async Task<EmbedDomainVerificationDto?> VerifyNowAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();
        var row = GetById(userId, verificationId);
        if (row == null) return null;

        var (verified, error) = await CheckVerificationAsync(row, cancellationToken);
        var now = DateTime.UtcNow;

        ExecuteWithCommand(
            """
            UPDATE "EmbedDomainVerifications"
            SET "Status" = @status,
                "LastError" = @lastError,
                "VerifiedAt" = @verifiedAt,
                "UpdatedDate" = @updatedDate
            WHERE "Id" = @id
              AND "UserId" = @userId
            """,
            command =>
            {
                AddParameter(command, "status", verified ? "verified" : "failed");
                AddParameter(command, "lastError", (object?)error ?? DBNull.Value);
                AddParameter(command, "verifiedAt", verified ? now : DBNull.Value);
                AddParameter(command, "updatedDate", now);
                AddParameter(command, "id", verificationId);
                AddParameter(command, "userId", userId);
                command.ExecuteNonQuery();
            });

        return GetById(userId, verificationId);
    }

    public async Task<EmbedDomainVerificationDto?> ActivateOriginAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken)
    {
        var row = await VerifyNowAsync(userId, verificationId, cancellationToken);
        if (row == null) return null;
        if (!row.Status.Equals("verified", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Domain doğrulanmadı. Önce doğrulamayı tamamlayın.");
        }

        var settings = await _embedOriginService.GetOrCreateAsync(userId, cancellationToken);
        var origins = settings.AllowedOrigins
            .Append(row.Origin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await _embedOriginService.UpsertAsync(userId, origins, settings.Theme, settings.Mode, cancellationToken);
        return row;
    }

    public Task<bool> DeleteAsync(Guid userId, Guid verificationId, CancellationToken cancellationToken)
    {
        EnsureStorageSchema();
        var affected = ExecuteWithCommand(
            """
            DELETE FROM "EmbedDomainVerifications"
            WHERE "Id" = @id
              AND "UserId" = @userId
            """,
            command =>
            {
                AddParameter(command, "id", verificationId);
                AddParameter(command, "userId", userId);
                return command.ExecuteNonQuery();
            });
        return Task.FromResult(affected > 0);
    }

    private async Task<(bool verified, string? error)> CheckVerificationAsync(EmbedDomainVerificationDto row, CancellationToken cancellationToken)
    {
        if (row.Method == "dns_txt")
        {
            return await VerifyDnsTxtAsync(row, cancellationToken);
        }

        return await VerifyFileAsync(row, cancellationToken);
    }

    private async Task<(bool verified, string? error)> VerifyDnsTxtAsync(EmbedDomainVerificationDto row, CancellationToken cancellationToken)
    {
        var recordName = $"_partalog-challenge.{row.Domain}";
        var endpoint = $"https://dns.google/resolve?name={Uri.EscapeDataString(recordName)}&type=TXT";
        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            var response = await http.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"DNS sorgusu başarısız ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Answer", out var answers) || answers.ValueKind != JsonValueKind.Array)
            {
                return (false, "TXT kaydı bulunamadı.");
            }

            foreach (var answer in answers.EnumerateArray())
            {
                if (!answer.TryGetProperty("data", out var dataElement)) continue;
                var data = dataElement.GetString() ?? string.Empty;
                var plain = data.Replace("\"", string.Empty).Trim();
                if (plain.Contains(row.ChallengeToken, StringComparison.Ordinal))
                {
                    return (true, null);
                }
            }

            return (false, "TXT içinde challenge token eşleşmedi.");
        }
        catch (Exception ex)
        {
            return (false, $"DNS doğrulama hatası: {ex.Message}");
        }
    }

    private async Task<(bool verified, string? error)> VerifyFileAsync(EmbedDomainVerificationDto row, CancellationToken cancellationToken)
    {
        var url = $"{row.Origin}/.well-known/partalog-verification.txt";
        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Dosya erişimi başarısız ({(int)response.StatusCode}).");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!content.Contains(row.ChallengeToken, StringComparison.Ordinal))
            {
                return (false, "Doğrulama dosyasında challenge token bulunamadı.");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Dosya doğrulama hatası: {ex.Message}");
        }
    }

    private EmbedDomainVerificationDto? GetById(Guid userId, Guid verificationId)
    {
        return ExecuteWithCommand(
            """
            SELECT "Id", "UserId", "Origin", "Domain", "Method", "ChallengeToken", "Status", "LastError", "CreatedDate", "UpdatedDate", "VerifiedAt"
            FROM "EmbedDomainVerifications"
            WHERE "Id" = @id
              AND "UserId" = @userId
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "id", verificationId);
                AddParameter(command, "userId", userId);
                using var reader = command.ExecuteReader();
                if (!reader.Read()) return null;
                return MapReader(reader);
            });
    }

    private EmbedDomainVerificationDto? GetByOrigin(Guid userId, string origin)
    {
        return ExecuteWithCommand(
            """
            SELECT "Id", "UserId", "Origin", "Domain", "Method", "ChallengeToken", "Status", "LastError", "CreatedDate", "UpdatedDate", "VerifiedAt"
            FROM "EmbedDomainVerifications"
            WHERE "UserId" = @userId
              AND "Origin" = @origin
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "userId", userId);
                AddParameter(command, "origin", origin);
                using var reader = command.ExecuteReader();
                if (!reader.Read()) return null;
                return MapReader(reader);
            });
    }

    private static EmbedDomainVerificationDto MapReader(DbDataReader reader)
    {
        return new EmbedDomainVerificationDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10));
    }

    private static string GenerateChallengeToken()
    {
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        return $"partalog-verify-{random}";
    }

    private static string? NormalizeMethod(string? method)
    {
        var value = (method ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "dns_txt" => "dns_txt",
            "file" => "file",
            _ => null
        };
    }

    private T ExecuteWithCommand<T>(string sql, Func<DbCommand, T> action)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) connection.Open();

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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = $"@{name}";
        parameter.Value = value;
        command.Parameters.Add(parameter);
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

    private void EnsureStorageSchema()
    {
        ExecuteWithCommand(
            """
            CREATE TABLE IF NOT EXISTS "EmbedDomainVerifications" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Origin" text NOT NULL,
                "Domain" text NOT NULL,
                "Method" text NOT NULL,
                "ChallengeToken" text NOT NULL,
                "Status" text NOT NULL,
                "LastError" text NULL,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "VerifiedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_EmbedDomainVerifications" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EmbedDomainVerifications_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmbedDomainVerifications_UserId_Origin"
            ON "EmbedDomainVerifications" ("UserId", "Origin");
            """,
            command => command.ExecuteNonQuery());
    }
}
