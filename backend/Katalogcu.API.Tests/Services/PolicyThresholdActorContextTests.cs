using System.Net;
using System.Security.Claims;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class PolicyThresholdActorContextTests
{
    [Theory]
    [InlineData("platformadmin")]
    [InlineData("admin")]
    [InlineData("owner")]
    public void CanManagePolicies_AllowsPrivilegedRoles(string role)
    {
        var context = CreateActorContext(role: role);

        Assert.True(context.CanManagePolicies);
    }

    [Fact]
    public void CanManagePolicies_RejectsUnprivilegedRole()
    {
        var context = CreateActorContext(role: "member");

        Assert.False(context.CanManagePolicies);
    }

    [Fact]
    public void IsPlatformAdmin_IsCaseInsensitive()
    {
        var context = CreateActorContext(role: "PlatformAdmin");

        Assert.True(context.IsPlatformAdmin);
    }

    [Fact]
    public void UserId_ReturnsEmptyGuidWhenClaimCannotBeParsed()
    {
        var context = CreateActorContext(userId: "not-a-guid", role: "admin");

        Assert.Equal(Guid.Empty, context.UserId);
    }

    [Fact]
    public void ActorEmail_UsesEmailThenNameThenAdminFallback()
    {
        var emailContext = CreateActorContext(email: "owner@example.com", name: "Owner Name");
        var nameContext = CreateActorContext(email: null, name: "Owner Name");
        var fallbackContext = CreateActorContext(email: null, name: null);

        Assert.Equal("owner@example.com", emailContext.ActorEmail);
        Assert.Equal("Owner Name", nameContext.ActorEmail);
        Assert.Equal("admin", fallbackContext.ActorEmail);
    }

    [Fact]
    public void BuildActor_CarriesHttpActorMetadata()
    {
        var userId = Guid.NewGuid();
        var context = CreateActorContext(
            userId: userId.ToString(),
            role: "platformadmin",
            email: "actor@example.com",
            remoteIpAddress: IPAddress.Parse("127.0.0.42"),
            userAgent: "KatalogcuTests/1.0");

        var actor = context.BuildActor();

        Assert.Equal(userId, actor.UserId);
        Assert.True(actor.IsPlatformAdmin);
        Assert.Equal("actor@example.com", actor.Email);
        Assert.Equal("platformadmin", actor.Role);
        Assert.Equal("127.0.0.42", actor.IpAddress);
        Assert.Equal("KatalogcuTests/1.0", actor.UserAgent);
    }

    private static PolicyThresholdActorContext CreateActorContext(
        string? userId = null,
        string? role = "admin",
        string? email = "admin@example.com",
        string? name = null,
        IPAddress? remoteIpAddress = null,
        string? userAgent = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        httpContext.Connection.RemoteIpAddress = remoteIpAddress;
        if (userAgent is not null)
        {
            httpContext.Request.Headers.UserAgent = userAgent;
        }

        return new PolicyThresholdActorContext(new TestHttpContextAccessor(httpContext));
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public TestHttpContextAccessor(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public HttpContext? HttpContext { get; set; }
    }
}
