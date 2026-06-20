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
        var events = new List<ChatStreamEventContract.ChatStreamEvent>();
        foreach (var line in dataLines)
        {
            Assert.True(ChatStreamEventContract.TryExtractDataPayload(line, out var payload));
            Assert.True(
                ChatStreamEventContract.TryParseDataPayload(payload, out var streamEvent, out var error),
                error);
            Assert.NotNull(streamEvent);
            events.Add(streamEvent);
        }

        Assert.Collection(
            events,
            streamEvent =>
            {
                Assert.Equal("sources", streamEvent.Type);
                Assert.False(streamEvent.Fallback.Used);
            },
            streamEvent =>
            {
                Assert.Equal("token", streamEvent.Type);
                Assert.Equal("Yedek yanit", streamEvent.Token);
                Assert.True(streamEvent.Fallback.Used);
                Assert.Equal("zero_tokens", streamEvent.Fallback.Reason);
            },
            streamEvent =>
            {
                Assert.Equal("done", streamEvent.Type);
                Assert.Equal(ChatStreamEventContract.CompletionStatus, streamEvent.Completion?.Status);
                Assert.True(streamEvent.Fallback.Used);
                Assert.Equal("zero_tokens", streamEvent.Fallback.Reason);
            });
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

    [Fact]
    public async Task ProxyAskStreamAsync_ReturnsFallbackContractWhenUpstreamFails()
    {
        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory("upstream unavailable", HttpStatusCode.ServiceUnavailable),
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

        var events = await ReadStreamEventsAsync(httpContext.Response.Body);

        AssertFallbackContract(events, "upstream_non_success");
    }

    [Fact]
    public async Task ProxyAskStreamAsync_ReturnsFallbackContractWhenUpstreamConnectionFails()
    {
        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(new HttpRequestException("connection refused")),
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

        var events = await ReadStreamEventsAsync(httpContext.Response.Body);

        AssertFallbackContract(events, "upstream_connection_failure");
    }

    private static async Task<List<ChatStreamEventContract.ChatStreamEvent>> ReadStreamEventsAsync(Stream responseBody)
    {
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var body = await reader.ReadToEndAsync();

        var events = new List<ChatStreamEventContract.ChatStreamEvent>();
        var dataLines = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal));

        foreach (var line in dataLines)
        {
            Assert.True(ChatStreamEventContract.TryExtractDataPayload(line, out var payload));
            Assert.True(
                ChatStreamEventContract.TryParseDataPayload(payload, out var streamEvent, out var error),
                error);
            Assert.NotNull(streamEvent);
            events.Add(streamEvent);
        }

        return events;
    }

    private static void AssertFallbackContract(
        IReadOnlyList<ChatStreamEventContract.ChatStreamEvent> events,
        string expectedReason)
    {
        Assert.Collection(
            events,
            streamEvent =>
            {
                Assert.Equal("sources", streamEvent.Type);
                Assert.True(streamEvent.Fallback.Used);
                Assert.Equal(expectedReason, streamEvent.Fallback.Reason);
            },
            streamEvent =>
            {
                Assert.Equal("token", streamEvent.Type);
                Assert.False(string.IsNullOrWhiteSpace(streamEvent.Token));
                Assert.True(streamEvent.Fallback.Used);
                Assert.Equal(expectedReason, streamEvent.Fallback.Reason);
            },
            streamEvent =>
            {
                Assert.Equal("done", streamEvent.Type);
                Assert.Equal(ChatStreamEventContract.CompletionStatus, streamEvent.Completion?.Status);
                Assert.True(streamEvent.Fallback.Used);
                Assert.Equal(expectedReason, streamEvent.Fallback.Reason);
            });
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _exception;

        public StubHttpClientFactory(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
            _exception = null;
        }

        public StubHttpClientFactory(Exception exception)
        {
            _responseBody = string.Empty;
            _statusCode = HttpStatusCode.OK;
            _exception = exception;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHandler(_responseBody, _statusCode, _exception))
            {
                BaseAddress = new Uri("http://localhost/")
            };
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _exception;

        public static string? LastRequestPath { get; private set; }
        public static string LastRequestBody { get; private set; } = string.Empty;

        public StubHandler(string responseBody, HttpStatusCode statusCode, Exception? exception)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
            _exception = exception;
            LastRequestPath = null;
            LastRequestBody = string.Empty;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_exception is not null)
            {
                throw _exception;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(_responseBody)))
            };
        }
    }
}
