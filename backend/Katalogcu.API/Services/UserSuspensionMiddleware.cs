using System.Security.Claims;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed class UserSuspensionMiddleware
{
    private readonly RequestDelegate _next;

    public UserSuspensionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (IsBypassPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("platformadmin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            await _next(context);
            return;
        }

        var currentRole = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (!string.IsNullOrWhiteSpace(currentRole) &&
            currentRole.Equals("suspendedowner", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Hesabınız askıya alındı. Destek ekibiyle iletişime geçin."
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool IsBypassPath(PathString path)
    {
        if (path.StartsWithSegments("/health")) return true;
        if (path.StartsWithSegments("/swagger")) return true;
        if (path.StartsWithSegments("/api/platform-auth")) return true;
        return false;
    }
}
