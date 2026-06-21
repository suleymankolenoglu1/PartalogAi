namespace Katalogcu.Application.Common.Interfaces;

public interface ISafeExternalHttpClient
{
    Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken);
}
