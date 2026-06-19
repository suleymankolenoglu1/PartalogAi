using System.Net;
using System.Text;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public class ChatStreamProxyServiceTests
{
    [Fact]
    public async Task ProxyAskStreamAsync_PreservesVersionedSseContractAcrossPassthrough()
    {
        const string upstreamBody =
            "data: {\"schemaVersion\":1,\"type\":\"sources\",\"sources\":[],\"fallback\":{\"used\":false,\"reason\":null}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"token\",\"token\":\"Yedek yanit\",\"fallback\":{\"used\":true,\"reason\":\"zero_tokens\"}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"done\",\"completion\":{\"status\":\"completed\"},\"fallback\":{\"used\":true,\"reason\":\"zero_tokens\"}}\n\n";

        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(upstreamBody),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            catalogIds: Array.Empty<string>(),
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var proxiedBody = await reader.ReadToEndAsync();

        Assert.Equal("text/event-stream", httpContext.Response.Headers.ContentType);
        Assert.Contains("\"schemaVersion\":1", proxiedBody);
        Assert.Contains("\"type\":\"token\"", proxiedBody);
        Assert.Contains("\"reason\":\"zero_tokens\"", proxiedBody);
        Assert.Contains("\"completion\":{\"status\":\"completed\"}", proxiedBody);

        var dataLines = proxiedBody
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, dataLines.Length);
        foreach (var line in dataLines)
        {
            Assert.True(ChatStreamEventContract.TryExtractDataPayload(line, out var payload));
            Assert.True(
                ChatStreamEventContract.TryParseDataPayload(payload, out var streamEvent, out var error),
                error);
            Assert.NotNull(streamEvent);
        }
    }

    [Fact]
    public async Task ProxyAskStreamAsync_ForwardsChatFormFieldsToPartalogAi()
    {
        var catalogId = Guid.NewGuid().ToString();
        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory("data: {\"schemaVersion\":1,\"type\":\"done\"}\n\n"),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[{\"role\":\"user\"}]",
            catalogIds: [catalogId],
            image: null,
            userPlan: "CatalogWithAI",
            aiLimitPerMonth: 100,
            aiUsedThisMonth: 7,
            cancellationToken: CancellationToken.None);

        Assert.Equal("/api/chat/stream", StubHandler.LastRequestPath);
        Assert.Contains("name=text", StubHandler.LastRequestBody);
        Assert.Contains("vida", StubHandler.LastRequestBody);
        Assert.Contains("name=history", StubHandler.LastRequestBody);
        Assert.Contains("\"role\":\"user\"", StubHandler.LastRequestBody);
        Assert.Contains("name=catalog_ids", StubHandler.LastRequestBody);
        Assert.Contains(catalogId, StubHandler.LastRequestBody);
        Assert.Contains("name=user_plan", StubHandler.LastRequestBody);
        Assert.Contains("CatalogWithAI", StubHandler.LastRequestBody);
        Assert.Contains("name=ai_limit_per_month", StubHandler.LastRequestBody);
        Assert.Contains("100", StubHandler.LastRequestBody);
        Assert.Contains("name=ai_used_this_month", StubHandler.LastRequestBody);
        Assert.Contains("7", StubHandler.LastRequestBody);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string _responseBody;

        public StubHttpClientFactory(string responseBody)
        {
            _responseBody = responseBody;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHandler(_responseBody))
            {
                BaseAddress = new Uri("http://localhost/")
            };
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public static string? LastRequestPath { get; private set; }
        public static string LastRequestBody { get; private set; } = string.Empty;

        public StubHandler(string responseBody)
        {
            _responseBody = responseBody;
            LastRequestPath = null;
            LastRequestBody = string.Empty;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(_responseBody)))
            };
        }
    }
}
