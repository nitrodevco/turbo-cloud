using System.Net;
using FluentAssertions;
using Turbo.Networking.Session;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// The address a game connection is from, which address bans and account caps decide on: a
/// forwarded header is believed only from a proxy the hotel trusts, so a client connecting
/// directly cannot name another address, and behind a chain of trusted proxies it is the first
/// hop none of them is.
/// </summary>
public sealed class ClientAddressTests
{
    [Theory]
    // Straight from the client: its own address; a header it sends itself is ignored.
    [InlineData("203.0.113.9", null, "203.0.113.9")]
    [InlineData("203.0.113.9", "10.1.2.3", "203.0.113.9")]
    // The proxy on the same machine: the address it heard from.
    [InlineData("127.0.0.1", "198.51.100.4", "198.51.100.4")]
    [InlineData("::1", "198.51.100.4:51234", "198.51.100.4")]
    [InlineData("::ffff:127.0.0.1", "198.51.100.4", "198.51.100.4")]
    // A client's own header in front of the proxy's is skipped: the nearest untrusted hop wins.
    [InlineData("127.0.0.1", "6.6.6.6, 198.51.100.4", "198.51.100.4")]
    // A configured proxy range in the chain is walked past.
    [InlineData("10.0.0.5", "198.51.100.4, 10.0.0.7", "198.51.100.4")]
    // Nothing usable in the header: the proxy's own address.
    [InlineData("127.0.0.1", "not-an-address", "127.0.0.1")]
    public void The_client_address_believes_only_trusted_proxies(
        string peer,
        string? forwardedFor,
        string expected
    ) =>
        new ClientAddresses(["10.0.0.0/8"])
            .Resolve(new IPEndPoint(IPAddress.Parse(peer), 30000), forwardedFor)
            .Should()
            .Be(expected);
}
