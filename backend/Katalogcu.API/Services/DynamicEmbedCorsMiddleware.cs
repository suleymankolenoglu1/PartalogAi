using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class DynamicEmbedCorsMiddleware
{
    private readonly RequestDelegate _next;

    public DynamicEmbedCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IEmbedOriginService embedOriginService,
        IPublicAccessTokenService publicAccessTokenService)
    {
        if (!context.Request.Path.StartsWithSegments("/api/embed"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/embed/settings"))
        {
            await _next(context);
            return;
        }

        var requestOrigin = context.Request.Headers.Origin.ToString();
        var normalizedOrigin = embedOriginService.NormalizeOrigin(requestOrigin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            await _next(context);
            return;
        }

        var isVerifyOriginEndpoint = context.Request.Path.StartsWithSegments("/api/embed/verify-origin");
        var isAllowed = isVerifyOriginEndpoint || await IsAllowedByTokenAsync(
            context,
            normalizedOrigin,
            embedOriginService,
            publicAccessTokenService);

        if (!isAllowed && HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (isAllowed)
        {
            ApplyCorsHeaders(context, normalizedOrigin);

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }

        await _next(context);
    }

    private static void ApplyCorsHeaders(HttpContext context, string normalizedOrigin)
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = normalizedOrigin;
        context.Response.Headers["Vary"] = "Origin";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type,Authorization,X-Public-Token";
        context.Response.Headers["Access-Control-Max-Age"] = "600";
    }

    private static async Task<bool> IsAllowedByTokenAsync(
        HttpContext context,
        string normalizedOrigin,
        IEmbedOriginService embedOriginService,
        IPublicAccessTokenService publicAccessTokenService)
    {
        var publicToken = ResolvePublicToken(context);
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            return false;
        }

        var payload = publicAccessTokenService.Validate(publicToken);
        if (payload == null || payload.UserId == Guid.Empty)
        {
            return false;
        }

        return await embedOriginService.IsOriginAllowedAsync(payload.UserId, normalizedOrigin, context.RequestAborted);
    }

    private static string? ResolvePublicToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Public-Token", out var fromHeader))
        {
            var token = fromHeader.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(token)) return token;
        }

        if (context.Request.Query.TryGetValue("token", out var fromTokenQuery))
        {
            var token = fromTokenQuery.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(token)) return token;
        }

        if (context.Request.Query.TryGetValue("publicToken", out var fromPublicTokenQuery))
        {
            var token = fromPublicTokenQuery.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(token)) return token;
        }

        return null;
    }
}
