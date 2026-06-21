using System.Net;
using System.Text;
using System.Text.Json;
using Katalogcu.API.Services;
using Katalogcu.Application.Features.Ai.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
            NullLogger<PartalogAiService>.Instance,
            Options.Create(new AiServiceOptions()));

        var response = await service.GetExpertChatResponseAsync(new AiChatRequestDto { Text = "yamato vida" });

        Assert.Equal(busyMessage, response.Answer);
        Assert.Empty(response.Sources ?? []);

        var debugIntentJson = JsonSerializer.Serialize(response.DebugIntent);
        Assert.Contains("\"fallback\":true", debugIntentJson);
        Assert.Contains("\"fallback_reason\":\"ai_capacity_limited\"", debugIntentJson);
    }

    [Fact]
    public async Task GetExpertChatResponseAsync_AllowsNullSourceSimilarity()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "answer": "İplik kılavuzu uygun adaydır.",
                  "sources": [
                    {
                      "code": "70003363",
                      "name": "İPLİK KILAVUZU",
                      "catalog_id": "531714e8-a10a-43d5-95fd-c227753f7546",
                      "similarity": null
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var service = new PartalogAiService(
            httpClient,
            NullLogger<PartalogAiService>.Instance,
            Options.Create(new AiServiceOptions()));

        var response = await service.GetExpertChatResponseAsync(new AiChatRequestDto { Text = "iplik geçirme mekanizması" });

        Assert.Equal("İplik kılavuzu uygun adaydır.", response.Answer);
        var source = Assert.Single(response.Sources ?? []);
        Assert.Equal("70003363", source.Code);
        Assert.Null(source.Similarity);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
