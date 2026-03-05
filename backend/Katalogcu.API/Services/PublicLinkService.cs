using System.Data;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Katalogcu.API.Services;

public record PublicLinkPayload(Guid UserId, List<Guid> CatalogIds);

public interface IPublicLinkService
{
    string CreateToken(Guid userId, int publicLinkVersion, IEnumerable<Guid>? catalogIds = null);
    PublicLinkPayload? Validate(string token);
}

public class PublicLinkService : IPublicLinkService
{
    private const string DbTokenPrefix = "pk_";
    private const string LegacyCompactPrefix = "pl.";

    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryDays;
    private readonly AppDbContext _context;
    private readonly ILogger<PublicLinkService> _logger;

    public PublicLinkService(IConfiguration config, AppDbContext context, ILogger<PublicLinkService> logger)
    {
        _context = context;
        _logger = logger;
        var secret = config["PublicLink:SecretKey"] ?? "";
        _issuer = config["PublicLink:Issuer"] ?? "KatalogcuPublic";
        _audience = config["PublicLink:Audience"] ?? "KatalogcuPublic";
        _expiryDays = int.TryParse(config["PublicLink:ExpirationDays"], out var days) ? days : 365;

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("PublicLink:SecretKey is missing.");
        }

        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string CreateToken(Guid userId, int publicLinkVersion, IEnumerable<Guid>? catalogIds = null)
    {
        EnsureStorageSchema();

        var ids = catalogIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        var catalogIdsCsv = ids.Count > 0 ? string.Join(",", ids) : null;
        var expiresAtUtc = DateTime.UtcNow.AddDays(_expiryDays);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = $"{DbTokenPrefix}{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(18))}";
            var tokenHash = ComputeTokenHash(token);
            if (TokenHashExists(tokenHash))
            {
                continue;
            }

            InsertTokenRecord(tokenHash, userId, publicLinkVersion, catalogIdsCsv, expiresAtUtc);
            return token;
        }

        throw new InvalidOperationException("Public link token oluşturulamadı. Lütfen tekrar deneyin.");
    }

    public PublicLinkPayload? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Public link validate failed: token empty");
            return null;
        }

        if (token.StartsWith(DbTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateDbToken(token);
        }

        // Geriye uyumluluk: eski JWT / compact tokenlar
        return ValidateLegacyToken(token);
    }

    private PublicLinkPayload? ValidateDbToken(string token)
    {
        try
        {
            EnsureStorageSchema();

            var tokenHash = ComputeTokenHash(token);
            var record = GetTokenRecord(tokenHash);
            if (record == null)
            {
                _logger.LogWarning("Public link validate failed: token not found");
                return null;
            }

            if (record.IsRevoked)
            {
                _logger.LogWarning("Public link validate failed: token revoked");
                return null;
            }

            if (record.ExpiresAtUtc < DateTime.UtcNow.AddMinutes(-2))
            {
                _logger.LogWarning("Public link validate failed: token expired");
                return null;
            }

            if (!ValidateUserState(record.UserId, record.PublicLinkVersion))
            {
                return null;
            }

            var catalogIds = ParseCatalogIdsCsv(record.CatalogIds);
            return new PublicLinkPayload(record.UserId, catalogIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public link validate failed: db token parse error");
            return null;
        }
    }

    private PublicLinkPayload? ValidateLegacyToken(string token)
    {
        try
        {
            if (token.StartsWith(LegacyCompactPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Legacy compact public token is no longer supported for creation. Rotate link to get new short token.");
                return null;
            }

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = _handler.ValidateToken(token, parameters, out _);
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrWhiteSpace(sub))
            {
                try
                {
                    sub = _handler.ReadJwtToken(token).Subject;
                }
                catch
                {
                    // no-op
                }
            }

            if (!Guid.TryParse(sub, out var userId))
            {
                _logger.LogWarning("Public link validate failed: invalid sub claim");
                return null;
            }

            var versionClaim = principal.FindFirst("plv")?.Value;
            var tokenVersion = 1;
            if (!string.IsNullOrWhiteSpace(versionClaim) && !int.TryParse(versionClaim, out tokenVersion))
            {
                _logger.LogWarning("Public link validate failed: invalid plv claim for user {UserId}", userId);
                return null;
            }

            if (!ValidateUserState(userId, tokenVersion))
            {
                return null;
            }

            var catClaim = principal.FindFirst("cat")?.Value;
            var catIds = ParseCatalogIdsCsv(catClaim);
            return new PublicLinkPayload(userId, catIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public link validate failed: token parse/signature/lifetime check error");
            return null;
        }
    }

    private bool ValidateUserState(Guid userId, int tokenVersion)
    {
        var userState = _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.PublicLinkVersion, u.PublicLinkEnabled })
            .FirstOrDefault();

        if (userState == null || !userState.PublicLinkEnabled || userState.PublicLinkVersion != tokenVersion)
        {
            _logger.LogWarning(
                "Public link validate failed: state mismatch user={UserId} tokenVersion={TokenVersion} dbVersion={DbVersion} enabled={Enabled}",
                userId,
                tokenVersion,
                userState?.PublicLinkVersion,
                userState?.PublicLinkEnabled
            );
            return false;
        }

        return true;
    }

    private bool TokenHashExists(string tokenHash)
    {
        return ExecuteWithCommand(
            """
            SELECT 1
            FROM "PublicAccessLinks"
            WHERE "TokenHash" = @tokenHash
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "tokenHash", tokenHash);
                var scalar = command.ExecuteScalar();
                return scalar != null && scalar != DBNull.Value;
            });
    }

    private void InsertTokenRecord(string tokenHash, Guid userId, int publicLinkVersion, string? catalogIdsCsv, DateTime expiresAtUtc)
    {
        ExecuteWithCommand(
            """
            INSERT INTO "PublicAccessLinks"
                ("Id", "TokenHash", "UserId", "PublicLinkVersion", "CatalogIds", "ExpiresAtUtc", "IsRevoked", "CreatedDate", "UpdatedDate")
            VALUES
                (@id, @tokenHash, @userId, @publicLinkVersion, @catalogIds, @expiresAtUtc, FALSE, @createdDate, NULL)
            """,
            command =>
            {
                AddParameter(command, "id", Guid.NewGuid());
                AddParameter(command, "tokenHash", tokenHash);
                AddParameter(command, "userId", userId);
                AddParameter(command, "publicLinkVersion", publicLinkVersion);
                AddParameter(command, "catalogIds", (object?)catalogIdsCsv ?? DBNull.Value);
                AddParameter(command, "expiresAtUtc", expiresAtUtc);
                AddParameter(command, "createdDate", DateTime.UtcNow);
                command.ExecuteNonQuery();
            });
    }

    private PublicAccessLinkRecord? GetTokenRecord(string tokenHash)
    {
        return ExecuteWithCommand(
            """
            SELECT "UserId", "PublicLinkVersion", "CatalogIds", "ExpiresAtUtc", "IsRevoked"
            FROM "PublicAccessLinks"
            WHERE "TokenHash" = @tokenHash
            LIMIT 1
            """,
            command =>
            {
                AddParameter(command, "tokenHash", tokenHash);

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                return new PublicAccessLinkRecord(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetBoolean(4));
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

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<Guid> ParseCatalogIdsCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new List<Guid>();
        }

        var result = new List<Guid>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private void EnsureStorageSchema()
    {
        ExecuteWithCommand(
            """
            CREATE TABLE IF NOT EXISTS "PublicAccessLinks" (
                "Id" uuid NOT NULL,
                "TokenHash" text NOT NULL,
                "UserId" uuid NOT NULL,
                "PublicLinkVersion" integer NOT NULL,
                "CatalogIds" text NULL,
                "ExpiresAtUtc" timestamp with time zone NOT NULL,
                "IsRevoked" boolean NOT NULL DEFAULT FALSE,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                CONSTRAINT "PK_PublicAccessLinks" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PublicAccessLinks_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PublicAccessLinks_TokenHash"
            ON "PublicAccessLinks" ("TokenHash");
            """,
            command => command.ExecuteNonQuery());

        ExecuteWithCommand(
            """
            CREATE INDEX IF NOT EXISTS "IX_PublicAccessLinks_UserId_ExpiresAtUtc"
            ON "PublicAccessLinks" ("UserId", "ExpiresAtUtc");
            """,
            command => command.ExecuteNonQuery());
    }

    private sealed record PublicAccessLinkRecord(
        Guid UserId,
        int PublicLinkVersion,
        string? CatalogIds,
        DateTime ExpiresAtUtc,
        bool IsRevoked);
}
