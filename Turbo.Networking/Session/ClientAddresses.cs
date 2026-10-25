using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;

namespace Turbo.Networking.Session;

/// <summary>
/// The address a client connected from. It is the connection's own unless that is a proxy the
/// hotel trusts (loopback always, and those in <c>Turbo:Networking:TrustedProxies</c>): then it is
/// the nearest address its <c>X-Forwarded-For</c> names that is not itself a trusted proxy, as
/// ASP.NET's forwarded headers read it for the site. A header from anyone else is ignored, so a
/// client cannot claim another address. Empty when the connection has none (a test).
/// </summary>
public sealed class ClientAddresses
{
    public const string FORWARDED_FOR = "X-Forwarded-For";

    /// <summary>Trusts a proxy on the same machine only: a host that registers none gets this.</summary>
    public static ClientAddresses LoopbackOnly { get; } = new([]);

    private readonly ImmutableArray<IPNetwork> _trusted;

    public ClientAddresses(IEnumerable<string>? trustedProxies)
    {
        var trusted = ImmutableArray.CreateBuilder<IPNetwork>();

        foreach (var entry in trustedProxies ?? [])
            if (TryParseNetwork(entry, out var network))
                trusted.Add(network);
            else
                Invalid.Add(entry);

        _trusted = trusted.ToImmutable();
    }

    /// <summary>The configured proxies that are neither an address nor a range, which are ignored.</summary>
    public List<string> Invalid { get; } = [];

    public string Resolve(EndPoint? connection, string? forwardedFor)
    {
        if (connection is not IPEndPoint { Address: var peer })
            return string.Empty;

        peer = Normalise(peer);

        if (!IsTrusted(peer) || string.IsNullOrWhiteSpace(forwardedFor))
            return peer.ToString();

        // Each proxy appends who it heard from, so the nearest hop is last. Walk back past the
        // ones the hotel trusts; the first it does not is the client.
        var hops = forwardedFor.Split(',', StringSplitOptions.TrimEntries);
        var client = peer;

        for (var i = hops.Length - 1; i >= 0; i--)
        {
            if (!TryParseHop(hops[i], out var hop))
                break;

            client = hop;

            if (!IsTrusted(hop))
                break;
        }

        return client.ToString();
    }

    private bool IsTrusted(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        foreach (var network in _trusted)
            if (network.Contains(address))
                return true;

        return false;
    }

    private static IPAddress Normalise(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    // A hop is an address, optionally with a port ("1.2.3.4:5678", "[::1]:80").
    private static bool TryParseHop(string hop, out IPAddress address)
    {
        if (IPEndPoint.TryParse(hop, out var endPoint))
        {
            address = Normalise(endPoint.Address);

            return true;
        }

        address = IPAddress.None;

        return false;
    }

    private static bool TryParseNetwork(string entry, out IPNetwork network)
    {
        entry = entry.Trim();

        if (entry.Contains('/', StringComparison.Ordinal))
            return IPNetwork.TryParse(entry, out network);

        if (IPAddress.TryParse(entry, out var address))
        {
            address = Normalise(address);
            network = new IPNetwork(
                address,
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128
            );

            return true;
        }

        network = default;

        return false;
    }
}
