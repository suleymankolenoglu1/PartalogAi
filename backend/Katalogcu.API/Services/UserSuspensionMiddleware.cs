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

        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            await WriteDeniedAsync(context, StatusCodes.Status401Unauthorized, "Kimlik bilgisi geçersiz.");
            return;
        }

        var currentRole = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(context.RequestAborted);

        var tokenRole = context.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentRole) ||
            !currentRole.Equals(tokenRole, StringComparison.OrdinalIgnoreCase))
        {
            await WriteDeniedAsync(context, StatusCodes.Status401Unauthorized, "Oturum rolü güncel değil veya kullanıcı bulunamadı.");
            return;
        }

        if (currentRole.Equals("suspendedowner", StringComparison.OrdinalIgnoreCase))
        {
            await WriteDeniedAsync(context, StatusCodes.Status403Forbidden, "Hesabınız askıya alındı. Destek ekibiyle iletişime geçin.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteDeniedAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { message }, context.RequestAborted);
    }

    private static bool IsBypassPath(PathString path)
    {
        if (path.StartsWithSegments("/health")) return true;
        if (path.StartsWithSegments("/swagger")) return true;
        if (path.StartsWithSegments("/api/platform-auth")) return true;
        return false;
    }
}
