using System.Net;
using System.Net.Sockets;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.ExternalSites.Commands;

namespace Katalogcu.Infrastructure.Services;

public sealed class SafeExternalHttpClient : ISafeExternalHttpClient, IDisposable
{
    private const int MaxRedirects = 5;
    private readonly SocketsHttpHandler _handler;
    private readonly HttpClient _client;

    public SafeExternalHttpClient()
    {
        _handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseProxy = false,
            ConnectCallback = ConnectToValidatedAddressAsync
        };
        _client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PartalogBot/1.0");
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        if (!ExternalSiteUrlSecurityValidator.TryCreateAllowedUri(url, out var currentUri) || currentUri is null)
        {
            throw new HttpRequestException("Only absolute HTTP(S) URLs are allowed.");
        }

        for (var redirectCount = 0; ; redirectCount++)
        {
            if ((await ExternalSiteUrlSecurityValidator.ResolveSafeAddressesAsync(currentUri.DnsSafeHost, cancellationToken)).Length == 0)
            {
                throw new HttpRequestException("The destination address is not publicly routable.");
            }

            using var request = new HttpRequestMessage(method, currentUri);
            var response = await _client.SendAsync(request, completionOption, cancellationToken);
            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                return response;
            }

            if (redirectCount >= MaxRedirects)
            {
                response.Dispose();
                throw new HttpRequestException("The external URL exceeded the redirect limit.");
            }

            var nextUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(currentUri, response.Headers.Location);
            response.Dispose();

            if (!ExternalSiteUrlSecurityValidator.TryCreateAllowedUri(nextUri.ToString(), out currentUri) || currentUri is null)
            {
                throw new HttpRequestException("The redirect target is not an allowed HTTP(S) URL.");
            }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static async ValueTask<Stream> ConnectToValidatedAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await ExternalSiteUrlSecurityValidator.ResolveSafeAddressesAsync(
            context.DnsEndPoint.Host,
            cancellationToken);

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                lastError = ex;
                socket.Dispose();
            }
        }

        throw new HttpRequestException("No safe public address could be reached.", lastError);
    }
}
