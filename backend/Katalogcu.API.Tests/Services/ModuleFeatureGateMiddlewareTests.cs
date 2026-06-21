using Katalogcu.API.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class ModuleFeatureGateMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_BlocksChatPath_WhenOnlyCatalogAnalysisIsEnabled()
    {
        var (context, nextCalled) = await InvokeAsync(
            "/api/chat/ask",
            new TestFeaturePolicy(chatbotEnabled: false, catalogAnalysisEnabled: true));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task InvokeAsync_AllowsChatPath_WhenChatbotIsEnabled()
    {
        var (context, nextCalled) = await InvokeAsync(
            "/api/chat/ask-stream",
            new TestFeaturePolicy(chatbotEnabled: true, catalogAnalysisEnabled: false));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task InvokeAsync_BlocksCatalogAnalysisPath_WhenOnlyChatbotIsEnabled()
    {
        var (context, nextCalled) = await InvokeAsync(
            "/api/catalogs/11111111-1111-1111-1111-111111111111/start-ai-process",
            new TestFeaturePolicy(chatbotEnabled: true, catalogAnalysisEnabled: false));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task InvokeAsync_AllowsCatalogAnalysisPath_WhenCatalogAnalysisIsEnabled()
    {
        var (context, nextCalled) = await InvokeAsync(
            "/api/catalogs/ai-jobs",
            new TestFeaturePolicy(chatbotEnabled: false, catalogAnalysisEnabled: true));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled());
    }

    private static async Task<(DefaultHttpContext Context, Func<bool> NextCalled)> InvokeAsync(
        string path,
        IProductFeaturePolicy featurePolicy)
    {
        var called = false;
        var middleware = new ModuleFeatureGateMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            featurePolicy);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);
        return (context, () => called);
    }

    private sealed class TestFeaturePolicy(bool chatbotEnabled, bool catalogAnalysisEnabled)
        : IProductFeaturePolicy
    {
        public bool AiEnabled => ChatbotEnabled || CatalogAnalysisEnabled;
        public bool ChatbotEnabled { get; } = chatbotEnabled;
        public bool CatalogAnalysisEnabled { get; } = catalogAnalysisEnabled;
        public bool EcommerceEnabled => false;
        public bool UpgradePromptsEnabled => false;
        public bool PlanManagementEnabled => false;
    }
}
