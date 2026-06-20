using System.Net;
using System.Net.Sockets;

namespace Katalogcu.Application.Features.ExternalSites.Commands;

internal static class ExternalSiteUrlSecurityValidator
{
    public static bool HasAllowedHttpScheme(string? value)
        => TryCreateAllowedUri(value, out _);

    public static async Task<bool> IsSafeExternalUrlAsync(string? value, CancellationToken cancellationToken)
    {
        if (!TryCreateAllowedUri(value, out var uri))
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(uri);

        if (IsLocalHost(uri.Host))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var directIp))
        {
            return !IsPrivateOrLocalAddress(directIp);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            return addresses.Length == 0 || addresses.All(address => !IsPrivateOrLocalAddress(address));
        }
        catch (SocketException)
        {
            // DNS çözümlemesi ortamdan etkilenebilir; çözümleyemiyorsak sentaktik kontrolle devam ediyoruz.
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateAllowedUri(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.Broadcast) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6Loopback))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6Multicast ||
                   IsUniqueLocalIpv6(address);
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] switch
        {
            0 => true,
            10 => true,
            127 => true,
            100 when bytes[1] is >= 64 and <= 127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] is >= 16 and <= 31 => true,
            192 when bytes[1] == 168 => true,
            198 when bytes[1] is 18 or 19 => true,
            >= 224 => true,
            _ => false
        };
    }

    private static bool IsUniqueLocalIpv6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 16 && (bytes[0] & 0xfe) == 0xfc;
    }
}
