using System.Net;
using System.Text;
using System.Text.Json;
using Katalogcu.API.Services;
using Katalogcu.Application.Features.Ai.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class PartalogAiServiceTests
{
    [Fact]
    public async Task GetExpertChatResponseAsync_MapsCapacityLimitToExplicitFallback()
    {
        const string busyMessage = "AI kapasitesi şu an dolu. Lütfen birkaç saniye sonra tekrar deneyin.";
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                "{\"detail\":{\"message\":\"" + busyMessage + "\",\"reason\":\"ai_capacity_limited\"}}",
                Encoding.UTF8,
                "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var service = new PartalogAiService(
            httpClient,
            NullLogger<PartalogAiService>.Instance);

        var response = await service.GetExpertChatResponseAsync(new AiChatRequestDto { Text = "yamato vida" });

        Assert.Equal(busyMessage, response.Answer);
        Assert.Empty(response.Sources ?? []);

        var debugIntentJson = JsonSerializer.Serialize(response.DebugIntent);
        Assert.Contains("\"fallback\":true", debugIntentJson);
        Assert.Contains("\"fallback_reason\":\"ai_capacity_limited\"", debugIntentJson);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
