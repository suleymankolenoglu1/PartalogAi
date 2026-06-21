using System.Net;
using System.Net.Sockets;

namespace Katalogcu.Application.Features.ExternalSites.Commands;

public static class ExternalSiteUrlSecurityValidator
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
        return (await ResolveSafeAddressesAsync(uri.DnsSafeHost, cancellationToken)).Length > 0;
    }

    public static bool TryCreateAllowedUri(string? value, out Uri? uri)
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

    public static async Task<IPAddress[]> ResolveSafeAddressesAsync(string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) || IsLocalHost(host))
        {
            return [];
        }

        if (IPAddress.TryParse(host, out var directIp))
        {
            return IsPrivateOrLocalAddress(directIp) ? [] : [directIp];
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.Length > 0 && addresses.All(address => !IsPrivateOrLocalAddress(address))
                ? addresses
                : [];
        }
        catch (SocketException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

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
            var ipv6Bytes = address.GetAddressBytes();
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6Multicast ||
                   IsUniqueLocalIpv6(address) ||
                   (ipv6Bytes[0] == 0x20 && ipv6Bytes[1] == 0x01 && ipv6Bytes[2] == 0x0d && ipv6Bytes[3] == 0xb8);
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
            192 when bytes[1] == 0 => true,
            192 when bytes[1] == 88 && bytes[2] == 99 => true,
            192 when bytes[1] == 168 => true,
            198 when bytes[1] is 18 or 19 => true,
            198 when bytes[1] == 51 && bytes[2] == 100 => true,
            203 when bytes[1] == 0 && bytes[2] == 113 => true,
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
