using System.Net;
using Katalogcu.API.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class CloudRunIdentityTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsIdentityToken_WhenCloudRunAuthenticationIsEnabled()
    {
        var provider = new StubTokenProvider("signed-id-token");
        var innerHandler = new CaptureHandler();
        var handler = new CloudRunIdentityTokenHandler(
            Options.Create(new AiServiceOptions
            {
                BaseUrl = "https://partalog-ai.example.run.app",
                UseCloudRunIdentityToken = true,
                CloudRunAudience = "https://partalog-ai.example.run.app/"
            }),
            provider)
        {
            InnerHandler = innerHandler
        };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://partalog-ai.example.run.app/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", innerHandler.AuthorizationScheme);
        Assert.Equal("signed-id-token", innerHandler.AuthorizationParameter);
        Assert.Equal("https://partalog-ai.example.run.app", provider.Audience);
    }

    [Fact]
    public async Task SendAsync_DoesNotRequestToken_WhenCloudRunAuthenticationIsDisabled()
    {
        var provider = new StubTokenProvider("unused");
        var innerHandler = new CaptureHandler();
        var handler = new CloudRunIdentityTokenHandler(
            Options.Create(new AiServiceOptions
            {
                BaseUrl = "http://partalog-ai:8000",
                UseCloudRunIdentityToken = false
            }),
            provider)
        {
            InnerHandler = innerHandler
        };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("http://partalog-ai:8000/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(innerHandler.AuthorizationScheme);
        Assert.Equal(0, provider.CallCount);
    }

    private sealed class StubTokenProvider(string token) : ICloudRunIdentityTokenProvider
    {
        public int CallCount { get; private set; }
        public string? Audience { get; private set; }

        public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
        {
            CallCount++;
            Audience = audience;
            return Task.FromResult(token);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
