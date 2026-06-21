using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public interface ICloudRunIdentityTokenProvider
{
    Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken);
}

public sealed class GoogleCloudRunIdentityTokenProvider : ICloudRunIdentityTokenProvider
{
    private readonly ConcurrentDictionary<string, Lazy<Task<OidcToken>>> _tokens =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetOrAdd(
            audience,
            static targetAudience => new Lazy<Task<OidcToken>>(
                async () =>
                {
                    var credential = await GoogleCredential.GetApplicationDefaultAsync();
                    return await credential.GetOidcTokenAsync(
                        OidcTokenOptions.FromTargetAudience(targetAudience),
                        CancellationToken.None);
                },
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        return await token.GetAccessTokenAsync(cancellationToken);
    }
}

public sealed class CloudRunIdentityTokenHandler : DelegatingHandler
{
    private readonly AiServiceOptions _options;
    private readonly ICloudRunIdentityTokenProvider _tokenProvider;

    public CloudRunIdentityTokenHandler(
        IOptions<AiServiceOptions> options,
        ICloudRunIdentityTokenProvider tokenProvider)
    {
        _options = options.Value;
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_options.UseCloudRunIdentityToken)
        {
            var audience = ResolveAudience(request);
            var token = await _tokenProvider.GetTokenAsync(audience, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private string ResolveAudience(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.CloudRunAudience))
        {
            return _options.CloudRunAudience.TrimEnd('/');
        }

        var baseUri = request.RequestUri?.IsAbsoluteUri == true
            ? request.RequestUri
            : new Uri(_options.BaseUrl, UriKind.Absolute);

        return baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
