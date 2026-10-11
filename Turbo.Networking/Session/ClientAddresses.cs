using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Turbo.Networking.Session;

/// <summary>
/// The address a player connected from. Behind a reverse proxy (nginx in front of the WebSocket)
/// the connection comes from the proxy, which names the client in <c>X-Forwarded-For</c>; that
/// header is believed only from a proxy the hotel trusts, since a client can send any header it
/// likes. Each proxy appends the address it heard from, so the right-most address that is not
/// itself a trusted proxy is the client's.
/// </summary>
public static class ClientAddresses
{
    public static IPAddress? Resolve(
        EndPoint? remote,
        string? forwardedFor,
        IReadOnlyCollection<IPAddress> trustedProxies
    )
    {
        var connected = (remote as IPEndPoint)?.Address;

        if (connected is null)
            return null;

        connected = Normalize(connected);

        if (string.IsNullOrWhiteSpace(forwardedFor) || !trustedProxies.Contains(connected))
            return connected;

        foreach (var hop in forwardedFor.Split(',').Reverse())
        {
            if (!IPAddress.TryParse(hop.Trim(), out var address))
                return connected;

            address = Normalize(address);

            if (!trustedProxies.Contains(address))
                return address;
        }

        return connected;
    }

    /// <summary>An IPv4 address carried in IPv6 is the IPv4 address.</summary>
    public static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    public static IReadOnlyCollection<IPAddress> Parse(IEnumerable<string> addresses) =>
        [
            .. addresses
                .Select(x => IPAddress.TryParse(x.Trim(), out var a) ? Normalize(a) : null)
                .OfType<IPAddress>(),
        ];
}
