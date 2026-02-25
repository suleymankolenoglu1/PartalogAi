namespace Katalogcu.API.Services;

public interface IChatStreamProxyService
{
    Task ProxyAskStreamAsync(
        HttpResponse response,
        string? text,
        string? history,
        IReadOnlyCollection<string> catalogIds,
        IFormFile? image,
        CancellationToken cancellationToken);
}

public sealed class ChatStreamProxyService : IChatStreamProxyService
{
    private const int StreamBufferSize = 4096;
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

        if (image != null)
        {
            var imageContent = new StreamContent(image.OpenReadStream());
            formContent.Add(imageContent, "file", image.FileName);
        }

        try
        {
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream") { Content = formContent };
            using var pythonResponse = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
            // Client disconnected or request aborted.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskStream proxy hatası");
            throw;
        }
    }
}
