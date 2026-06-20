using System.Security.Claims;
using Katalogcu.Application.Features.PolicyThresholds.Common;

namespace Katalogcu.API.Services;

public interface IPolicyThresholdActorContext
{
    bool CanManagePolicies { get; }
    bool IsPlatformAdmin { get; }
    Guid UserId { get; }
    string ActorEmail { get; }
    PolicyThresholdActor BuildActor();
}

public sealed class PolicyThresholdActorContext : IPolicyThresholdActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PolicyThresholdActorContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanManagePolicies
    {
        get
        {
            var roles = CurrentUser.FindAll(ClaimTypes.Role).Select(x => x.Value);
            return roles.Any(role =>
                role.Equals("platformadmin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("owner", StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool IsPlatformAdmin => CurrentUser.FindAll(ClaimTypes.Role)
        .Any(x => x.Value.Equals("platformadmin", StringComparison.OrdinalIgnoreCase));

    public Guid UserId
    {
        get
        {
            var claim = CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }
    }

    public string ActorEmail => CurrentUser.FindFirst(ClaimTypes.Email)?.Value
                                ?? CurrentUser.FindFirst(ClaimTypes.Name)?.Value
                                ?? "admin";

    public PolicyThresholdActor BuildActor()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return new PolicyThresholdActor(
            UserId,
            IsPlatformAdmin,
            ActorEmail,
            CurrentUser.FindFirst(ClaimTypes.Role)?.Value,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString());
    }

    private ClaimsPrincipal CurrentUser => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
}
