using System.Net;
using System.Text;
using Katalogcu.API.Services;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Chat.Common;
using Katalogcu.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public class ChatStreamProxyServiceTests
{
    [Fact]
    public async Task ProxyAskStreamAsync_PreservesVersionedContractAcrossPassthrough()
    {
        const string upstreamBody =
            "data: {\"schemaVersion\":1,\"type\":\"sources\",\"sources\":[],\"searchTrace\":{\"original_query\":\"vida\",\"rewritten_query\":{\"text\":\"vida\",\"source\":\"fallback\"},\"resolved_scope\":{\"catalog_id\":null,\"catalog_ids\":[],\"brand\":null,\"machine_model\":null,\"scope_source\":\"none\"},\"retrieved_candidates_count\":0,\"compatibility_gate_filtered_count\":0,\"final_decision\":{\"decision\":\"LOW_CONFIDENCE\",\"candidate_scores\":[]}},\"fallback\":{\"used\":false,\"reason\":null}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"token\",\"token\":\"Yedek yanit\",\"fallback\":{\"used\":true,\"reason\":\"zero_tokens\"}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"done\",\"completion\":{\"status\":\"completed\"},\"fallback\":{\"used\":true,\"reason\":\"zero_tokens\"}}\n\n";

        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(upstreamBody),
            new StubChatQueryService(),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var result = await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            contextJson: null,
            catalogIds: Array.Empty<string>(),
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.Billable);
        Assert.Equal("zero_tokens", result.FallbackReason);
        Assert.Equal(3, result.EventCount);
        Assert.Equal(0, result.SourceCount);
        Assert.Equal(1, result.TokenEventCount);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var proxiedBody = await reader.ReadToEndAsync();

        Assert.Contains("\"schemaVersion\":1", proxiedBody);
        Assert.Contains("\"type\":\"token\"", proxiedBody);
        Assert.Contains("\"searchTrace\":", proxiedBody);
        Assert.Contains("\"original_query\":\"vida\"", proxiedBody);
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
    public async Task ProxyAskStreamAsync_EnrichesSourceEventsWithCatalogProductFields()
    {
        var catalogId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        const string upstreamBody =
            "data: {\"schemaVersion\":1,\"type\":\"sources\",\"sources\":[{\"code\":\"160000\",\"query\":\"vida\",\"similarity\":0.82}],\"fallback\":{\"used\":false,\"reason\":null}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"token\",\"token\":\"Vida bulundu.\",\"fallback\":{\"used\":false,\"reason\":null}}\n\n" +
            "data: {\"schemaVersion\":1,\"type\":\"done\",\"completion\":{\"status\":\"completed\"},\"fallback\":{\"used\":false,\"reason\":null}}\n\n";

        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(upstreamBody),
            new StubChatQueryService([
                new EnrichedPartDto
                {
                    Id = itemId,
                    Code = "160000",
                    RefNumber = "12",
                    Name = "Vida",
                    Brand = "YAMATO",
                    CatalogId = catalogId,
                    PageNumber = "5",
                    Model = "VG2500-8F",
                    StockStatus = "Stokta Var",
                    Price = 12.5m,
                    ImageUrl = "/img/vida.jpg"
                }
            ]),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var result = await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            contextJson: null,
            catalogIds: [catalogId.ToString()],
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Billable);
        Assert.Null(result.FallbackReason);
        Assert.Equal(3, result.EventCount);
        Assert.Equal(1, result.SourceCount);
        Assert.Equal(1, result.TokenEventCount);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var proxiedBody = await reader.ReadToEndAsync();

        Assert.Contains("\"catalogItemId\":\"" + itemId, proxiedBody);
        Assert.Contains("\"stockStatus\":\"Stokta Var\"", proxiedBody);
        Assert.Contains("\"price\":12.5", proxiedBody);
        Assert.Contains("\"pageNumber\":\"5\"", proxiedBody);
        Assert.Contains("\"similarity\":0.82", proxiedBody);
    }

    [Fact]
    public async Task ProxyAskStreamAsync_MapsUpstreamNonSuccessToFallbackSse()
    {
        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent(
                    "{\"detail\":{\"message\":\"AI servisi geçici olarak yanıt veremiyor.\",\"reason\":\"ai_upstream_error\"}}",
                    Encoding.UTF8,
                    "application/json")
            }),
            new StubChatQueryService(),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var result = await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            contextJson: null,
            catalogIds: Array.Empty<string>(),
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.Billable);
        Assert.Equal("ai_upstream_error", result.FallbackReason);
        Assert.Equal(3, result.EventCount);
        Assert.Equal(0, result.SourceCount);
        Assert.Equal(1, result.TokenEventCount);

        var proxiedBody = await ReadResponseBodyAsync(httpContext.Response);

        Assert.Contains("\"type\":\"sources\"", proxiedBody);
        Assert.Contains("\"type\":\"token\"", proxiedBody);
        Assert.Contains("\"type\":\"done\"", proxiedBody);
        Assert.Contains("\"used\":true", proxiedBody);
        Assert.Contains("\"reason\":\"ai_upstream_error\"", proxiedBody);
        Assert.Contains("AI servisi ge", proxiedBody);
    }

    [Fact]
    public async Task ProxyAskStreamAsync_MapsUpstreamTimeoutToFallbackSse()
    {
        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(_ => throw new OperationCanceledException("upstream timeout")),
            new StubChatQueryService(),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var result = await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            contextJson: null,
            catalogIds: Array.Empty<string>(),
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.Billable);
        Assert.Equal("ai_timeout", result.FallbackReason);
        Assert.Equal(3, result.EventCount);
        Assert.Equal(0, result.SourceCount);
        Assert.Equal(1, result.TokenEventCount);

        var proxiedBody = await ReadResponseBodyAsync(httpContext.Response);

        Assert.Contains("\"reason\":\"ai_timeout\"", proxiedBody);
        Assert.Contains("zaman a", proxiedBody);
    }

    [Fact]
    public async Task ProxyAskStreamAsync_DoesNotWriteFallbackWhenClientDisconnects()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var proxy = new ChatStreamProxyService(
            new StubHttpClientFactory(_ => throw new OperationCanceledException("client disconnected")),
            new StubChatQueryService(),
            NullLogger<ChatStreamProxyService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var result = await proxy.ProxyAskStreamAsync(
            httpContext.Response,
            text: "vida",
            history: "[]",
            contextJson: null,
            catalogIds: Array.Empty<string>(),
            image: null,
            userPlan: null,
            aiLimitPerMonth: null,
            aiUsedThisMonth: null,
            cancellationToken: cts.Token);

        Assert.False(result.Billable);
        Assert.Equal("client_disconnected", result.FallbackReason);
        Assert.Equal(0, result.EventCount);
        Assert.Equal(0, result.SourceCount);
        Assert.Equal(0, result.TokenEventCount);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpClientFactory(string responseBody)
            : this(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(responseBody)))
            })
        {
        }

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHandler(_responder))
            {
                BaseAddress = new Uri("http://localhost/")
            };
        }
    }

    private sealed class StubChatQueryService : IChatQueryService
    {
        private readonly IReadOnlyList<EnrichedPartDto> _enrichedParts;

        public StubChatQueryService(IReadOnlyList<EnrichedPartDto>? enrichedParts = null)
        {
            _enrichedParts = enrichedParts ?? [];
        }

        public Task<IReadOnlyList<Guid>> ResolveAccessibleCatalogIdsAsync(
            Guid tokenUserId,
            Guid? publicUserId,
            IReadOnlyCollection<Guid>? publicAllowedCatalogIds,
            IReadOnlyCollection<Guid> requestedCatalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }

        public Task<IReadOnlyList<EnrichedPartDto>> EnrichPythonSourcesAsync(
            IReadOnlyCollection<ChatSourceInput> sources,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_enrichedParts);
        }

        public Task<IReadOnlyList<CatalogItem>> SearchByCodeAsync(
            string? term,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CatalogItem>>([]);
        }

        public Task<IReadOnlyList<CatalogItem>> SearchByRefNumberAsync(
            string? term,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CatalogItem>>([]);
        }

        public Task<IReadOnlyList<CatalogItem>> SearchByNameAsync(
            string? term,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CatalogItem>>([]);
        }

        public Task<IReadOnlyList<EnrichedPartDto>> EnrichResultsAsync(
            IReadOnlyCollection<CatalogItem> items,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EnrichedPartDto>>([]);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
