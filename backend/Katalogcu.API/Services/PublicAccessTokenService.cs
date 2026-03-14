using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Katalogcu.API.Services;

public sealed class PublicAccessTokenService : IPublicAccessTokenService
{
    private const string EmbedKeyClaim = "embed_key";
    private const string CatalogIdsClaim = "catalog_ids";

    private readonly IPublicLinkService _publicLinkService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public PublicAccessTokenService(
        IPublicLinkService publicLinkService,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _publicLinkService = publicLinkService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public PublicAccessPayloadDto? Validate(string token)
    {
        var payload = _publicLinkService.Validate(token);
        if (payload != null)
        {
            return new PublicAccessPayloadDto
            {
                UserId = payload.UserId,
                CatalogIds = payload.CatalogIds
            };
        }

        return ValidateEmbedSession(token);
    }

    public string CreateEmbedSessionToken(Guid userId, IReadOnlyList<Guid> catalogIds, string embedKey, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEmbedSecretKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var notBefore = DateTime.UtcNow.AddMinutes(-1);
        var expires = expiresAtUtc.Kind == DateTimeKind.Utc ? expiresAtUtc : expiresAtUtc.ToUniversalTime();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(EmbedKeyClaim, embedKey),
            new(CatalogIdsClaim, string.Join(',', catalogIds.Distinct()))
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: GetEmbedIssuer(),
            audience: GetEmbedAudience(),
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    private PublicAccessPayloadDto? ValidateEmbedSession(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = GetEmbedIssuer(),
                ValidateAudience = true,
                ValidAudience = GetEmbedAudience(),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEmbedSecretKey())),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var jwt = tokenHandler.ReadJwtToken(token);
            var userIdRaw = jwt.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            var embedKey = jwt.Claims.FirstOrDefault(x => x.Type == EmbedKeyClaim)?.Value?.Trim();
            var catalogIdsRaw = jwt.Claims.FirstOrDefault(x => x.Type == CatalogIdsClaim)?.Value ?? string.Empty;
            var expUnix = jwt.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;

            if (!Guid.TryParse(userIdRaw, out var userId) || string.IsNullOrWhiteSpace(embedKey))
            {
                return null;
            }

            var catalogIds = catalogIdsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var parsed) ? parsed : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

            if (catalogIds.Length == 0)
            {
                return null;
            }

            var state = _dbContext.EmbedTargets
                .AsNoTracking()
                .Where(x => x.EmbedKey == embedKey)
                .Join(
                    _dbContext.Users.AsNoTracking(),
                    target => target.UserId,
                    user => user.Id,
                    (target, user) => new
                    {
                        target.UserId,
                        target.CatalogId,
                        target.IsActive,
                        target.AccessExpiresAt,
                        user.PublicLinkEnabled,
                        user.Role,
                        user.PlanExpiresAt
                    })
                .SingleOrDefault();

            if (state == null || state.UserId != userId || !state.IsActive)
            {
                return null;
            }

            if (!state.PublicLinkEnabled)
            {
                return null;
            }

            if (string.Equals(state.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var nowUtc = DateTime.UtcNow;
            if (state.PlanExpiresAt.HasValue && state.PlanExpiresAt.Value <= nowUtc)
            {
                return null;
            }

            if (state.AccessExpiresAt.HasValue && state.AccessExpiresAt.Value <= nowUtc)
            {
                return null;
            }

            if (!catalogIds.Contains(state.CatalogId))
            {
                return null;
            }

            DateTime? expiresAtUtc = null;
            if (long.TryParse(expUnix, out var exp))
            {
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }

            return new PublicAccessPayloadDto
            {
                UserId = userId,
                CatalogIds = catalogIds,
                IsEmbedSession = true,
                EmbedKey = embedKey,
                ExpiresAtUtc = expiresAtUtc
            };
        }
        catch
        {
            return null;
        }
    }

    private string GetEmbedSecretKey()
    {
        var configured = _configuration["EmbedAccessToken:SecretKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return _configuration["PublicLink:SecretKey"]
            ?? throw new InvalidOperationException("EmbedAccessToken:SecretKey veya PublicLink:SecretKey zorunludur.");
    }

    private string GetEmbedIssuer() => _configuration["EmbedAccessToken:Issuer"]?.Trim() ?? "KatalogcuEmbed";

    private string GetEmbedAudience() => _configuration["EmbedAccessToken:Audience"]?.Trim() ?? "KatalogcuEmbed";
}
