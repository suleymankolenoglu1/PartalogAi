using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Katalogcu.API.Services;

public sealed class CatalogPageAccessPayload
{
    public Guid PageId { get; init; }
    public Guid CatalogId { get; init; }
    public Guid OwnerUserId { get; init; }
    public bool IsPublic { get; init; }
    public IReadOnlyList<Guid> AllowedCatalogIds { get; init; } = [];
    public DateTime? ExpiresAtUtc { get; init; }
}

public interface ICatalogPageAccessTokenService
{
    string CreateToken(
        Guid pageId,
        Guid catalogId,
        Guid ownerUserId,
        bool isPublic,
        IReadOnlyCollection<Guid>? allowedCatalogIds = null,
        DateTime? expiresAtUtc = null);

    CatalogPageAccessPayload? Validate(string token);
}

public sealed class CatalogPageAccessTokenService : ICatalogPageAccessTokenService
{
    private const string PageIdClaim = "page_id";
    private const string CatalogIdClaim = "catalog_id";
    private const string OwnerUserIdClaim = "owner_user_id";
    private const string IsPublicClaim = "is_public";
    private const string AllowedCatalogIdsClaim = "allowed_catalog_ids";

    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public CatalogPageAccessTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(
        Guid pageId,
        Guid catalogId,
        Guid ownerUserId,
        bool isPublic,
        IReadOnlyCollection<Guid>? allowedCatalogIds = null,
        DateTime? expiresAtUtc = null)
    {
        var expires = expiresAtUtc?.ToUniversalTime() ?? DateTime.UtcNow.AddMinutes(15);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSecretKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var normalizedCatalogIds = (allowedCatalogIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        var claims = new List<Claim>
        {
            new(PageIdClaim, pageId.ToString()),
            new(CatalogIdClaim, catalogId.ToString()),
            new(OwnerUserIdClaim, ownerUserId.ToString()),
            new(IsPublicClaim, isPublic ? "1" : "0"),
            new(AllowedCatalogIdsClaim, string.Join(',', normalizedCatalogIds))
        };

        var token = new JwtSecurityToken(
            issuer: GetIssuer(),
            audience: GetAudience(),
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires,
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    public CatalogPageAccessPayload? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_tokenHandler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = GetIssuer(),
                ValidateAudience = true,
                ValidAudience = GetAudience(),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSecretKey())),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var jwt = _tokenHandler.ReadJwtToken(token);
            var pageIdRaw = jwt.Claims.FirstOrDefault(x => x.Type == PageIdClaim)?.Value;
            var catalogIdRaw = jwt.Claims.FirstOrDefault(x => x.Type == CatalogIdClaim)?.Value;
            var ownerUserIdRaw = jwt.Claims.FirstOrDefault(x => x.Type == OwnerUserIdClaim)?.Value;
            var isPublicRaw = jwt.Claims.FirstOrDefault(x => x.Type == IsPublicClaim)?.Value;
            var allowedCatalogIdsRaw = jwt.Claims.FirstOrDefault(x => x.Type == AllowedCatalogIdsClaim)?.Value ?? string.Empty;
            var expUnix = jwt.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;

            if (!Guid.TryParse(pageIdRaw, out var pageId) ||
                !Guid.TryParse(catalogIdRaw, out var catalogId) ||
                !Guid.TryParse(ownerUserIdRaw, out var ownerUserId))
            {
                return null;
            }

            var allowedCatalogIds = allowedCatalogIdsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var parsed) ? parsed : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

            DateTime? expiresAtUtc = null;
            if (long.TryParse(expUnix, out var exp))
            {
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }

            return new CatalogPageAccessPayload
            {
                PageId = pageId,
                CatalogId = catalogId,
                OwnerUserId = ownerUserId,
                IsPublic = string.Equals(isPublicRaw, "1", StringComparison.Ordinal),
                AllowedCatalogIds = allowedCatalogIds,
                ExpiresAtUtc = expiresAtUtc
            };
        }
        catch
        {
            return null;
        }
    }

    private string GetSecretKey()
    {
        return _configuration["CatalogPageAccessToken:SecretKey"]
            ?? _configuration["PublicLink:SecretKey"]
            ?? _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("Catalog page access token secret could not be resolved.");
    }

    private string GetIssuer() => _configuration["CatalogPageAccessToken:Issuer"]?.Trim() ?? "KatalogcuCatalogPage";

    private string GetAudience() => _configuration["CatalogPageAccessToken:Audience"]?.Trim() ?? "KatalogcuCatalogPage";
}
