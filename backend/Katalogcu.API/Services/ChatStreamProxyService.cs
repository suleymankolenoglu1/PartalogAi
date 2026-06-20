namespace Katalogcu.API.Services;

public interface IChatStreamProxyService
{
    Task ProxyAskStreamAsync(
        HttpResponse response,
        string? text,
        string? history,
        IReadOnlyCollection<string> catalogIds,
        IFormFile? image,
        string? userPlan,
        int? aiLimitPerMonth,
        int? aiUsedThisMonth,
        CancellationToken cancellationToken);
}

public sealed class ChatStreamProxyService : IChatStreamProxyService
{
    private const int StreamBufferSize = 4096;
    private const string UpstreamNonSuccessFallbackReason = "upstream_non_success";
    private const string UpstreamTimeoutFallbackReason = "upstream_timeout";
    private const string UpstreamConnectionFallbackReason = "upstream_connection_failure";
    private const string UpstreamUnexpectedFallbackReason = "upstream_unexpected_error";
    private const string FallbackMessage = "AI servisi su anda yanit veremiyor. Lutfen tekrar deneyin.";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatStreamProxyService> _logger;

    public ChatStreamProxyService(IHttpClientFactory httpClientFactory, ILogger<ChatStreamProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProxyAskStreamAsync(
        HttpResponse response,
        string? text,
        string? history,
        IReadOnlyCollection<string> catalogIds,
        IFormFile? image,
        string? userPlan,
        int? aiLimitPerMonth,
        int? aiUsedThisMonth,
        CancellationToken cancellationToken)
    {
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";

        var httpClient = _httpClientFactory.CreateClient("PartalogAi");

        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StringContent(text ?? string.Empty), "text");
        formContent.Add(new StringContent(history ?? "[]"), "history");
        formContent.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(catalogIds)), "catalog_ids");
        if (!string.IsNullOrWhiteSpace(userPlan))
        {
            formContent.Add(new StringContent(userPlan), "user_plan");
        }
        if (aiLimitPerMonth.HasValue)
        {
            formContent.Add(new StringContent(aiLimitPerMonth.Value.ToString()), "ai_limit_per_month");
        }
        if (aiUsedThisMonth.HasValue)
        {
            formContent.Add(new StringContent(aiUsedThisMonth.Value.ToString()), "ai_used_this_month");
        }

        if (image != null)
        {
            var imageContent = new StreamContent(image.OpenReadStream());
            formContent.Add(imageContent, "file", image.FileName);
        }

        try
        {
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream") { Content = formContent };
            using var pythonResponse = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!pythonResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AskStream upstream returned non-success status {StatusCode}",
                    (int)pythonResponse.StatusCode);
                await WriteFallbackStreamAsync(response, UpstreamNonSuccessFallbackReason, cancellationToken);
                return;
            }

            using var stream = await pythonResponse.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[StreamBufferSize];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected or request aborted.
                return;
            }

            _logger.LogWarning("AskStream upstream timed out");
            await WriteFallbackStreamAsync(response, UpstreamTimeoutFallbackReason, CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AskStream upstream connection failed");
            await WriteFallbackStreamAsync(response, UpstreamConnectionFallbackReason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskStream proxy hatası");
            await WriteFallbackStreamAsync(response, UpstreamUnexpectedFallbackReason, cancellationToken);
        }
    }

    private static async Task WriteFallbackStreamAsync(
        HttpResponse response,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        await WriteSseEventAsync(
            response,
            ChatStreamEventContract.CreateSources(
                Array.Empty<object>(),
                fallbackUsed: true,
                fallbackReason: fallbackReason),
            cancellationToken);
        await WriteSseEventAsync(
            response,
            ChatStreamEventContract.CreateToken(
                FallbackMessage,
                fallbackUsed: true,
                fallbackReason: fallbackReason),
            cancellationToken);
        await WriteSseEventAsync(
            response,
            ChatStreamEventContract.CreateDone(
                fallbackUsed: true,
                fallbackReason: fallbackReason),
            cancellationToken);
    }

    private static async Task WriteSseEventAsync(
        HttpResponse response,
        ChatStreamEventContract.ChatStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        var line = ChatStreamEventContract.ToSseDataLine(streamEvent);
        await response.WriteAsync(line, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
