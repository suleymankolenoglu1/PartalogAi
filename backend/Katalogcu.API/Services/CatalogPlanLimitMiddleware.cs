using System.Security.Claims;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Services;

public sealed class CatalogPlanLimitMiddleware
{
    private readonly RequestDelegate _next;

    public CatalogPlanLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (!IsCatalogCreateRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId) || userId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        var planValue = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.SubscriptionPlan)
            .FirstOrDefaultAsync(context.RequestAborted);
        if (!planValue.HasValue)
        {
            await _next(context);
            return;
        }

        var planLimits = PlanLimitRules.For((Katalogcu.Domain.Enums.SubscriptionPlan)planValue.Value);
        if (planLimits.MaxCatalogCount is null)
        {
            await _next(context);
            return;
        }

        var existingCatalogCount = await dbContext.Catalogs
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId, context.RequestAborted);

        if (existingCatalogCount >= planLimits.MaxCatalogCount.Value)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Plan limitinize ulaştınız"
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool IsCatalogCreateRequest(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method)) return false;
        var path = request.Path.Value?.TrimEnd('/');
        return string.Equals(path, "/api/catalogs", StringComparison.OrdinalIgnoreCase);
    }
}
