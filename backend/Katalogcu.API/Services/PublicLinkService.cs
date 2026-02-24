using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("plv", publicLinkVersion.ToString())
        };

        var ids = catalogIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (ids.Any())
        {
            claims.Add(new Claim("cat", string.Join(",", ids)));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(_expiryDays),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _handler.CreateToken(tokenDescriptor);
        return _handler.WriteToken(token);
    }

    public PublicLinkPayload? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Public link validate failed: token empty");
            return null;
        }

        try
        {
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

            // Bazı .NET JWT mapping ayarlarında "sub" claim'i NameIdentifier'a maplenebiliyor.
            // Yukarıdaki claim lookup ile bulunamazsa raw JWT subject'ten son kez okumayı dene.
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
                return null;
            }

            var catClaim = principal.FindFirst("cat")?.Value;
            var catIds = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(catClaim))
            {
                foreach (var part in catClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Guid.TryParse(part, out var gid))
                    {
                        catIds.Add(gid);
                    }
                }
            }

            return new PublicLinkPayload(userId, catIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public link validate failed: token parse/signature/lifetime check error");
            return null;
        }
    }
}
