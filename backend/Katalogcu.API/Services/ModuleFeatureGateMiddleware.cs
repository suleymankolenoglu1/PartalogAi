using System.Text.Json;

namespace Katalogcu.API.Services;

public sealed class ModuleFeatureGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProductFeaturePolicy _featurePolicy;

    public ModuleFeatureGateMiddleware(RequestDelegate next, IProductFeaturePolicy featurePolicy)
    {
        _next = next;
        _featurePolicy = featurePolicy;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (!_featurePolicy.ChatbotEnabled && IsChatbotPath(path))
        {
            await WriteBlockedResponseAsync(context, "Chatbot modülü bu yayında kapalı.");
            return;
        }

        if (!_featurePolicy.CatalogAnalysisEnabled && IsCatalogAnalysisPath(path))
        {
            await WriteBlockedResponseAsync(context, "Katalog AI analizi bu yayında kapalı.");
            return;
        }

        if (!_featurePolicy.EcommerceEnabled && IsEcommercePath(path))
        {
            await WriteBlockedResponseAsync(context, "E-ticaret modülü bu yayında kapalı.");
            return;
        }

        await _next(context);
    }

    private static bool IsChatbotPath(PathString path)
    {
        if (path.StartsWithSegments("/api/chat")) return true;
        if (path.StartsWithSegments("/api/chatfeedback")) return true;
        if (path.StartsWithSegments("/api/visualfeedback")) return true;
        return false;
    }

    private static bool IsCatalogAnalysisPath(PathString path)
    {
        if (path.StartsWithSegments("/api/ai")) return true;
        if (path.StartsWithSegments("/api/catalogs/ai-jobs")) return true;

        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/catalogs/", StringComparison.OrdinalIgnoreCase) &&
               value.EndsWith("/start-ai-process", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEcommercePath(PathString path)
    {
        if (path.StartsWithSegments("/api/orders")) return true;
        if (path.StartsWithSegments("/api/products")) return true;
        return false;
    }

    private static async Task WriteBlockedResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new { success = false, message });
        await context.Response.WriteAsync(payload);
    }
}
