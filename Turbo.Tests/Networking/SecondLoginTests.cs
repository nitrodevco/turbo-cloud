using FluentAssertions;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A connection logs in once. A second SSO ticket on a logged-in connection used to rebind it to
/// the ticket's player while the first stayed bound to it: the first player was online for good,
/// never announced offline, and their composers went to the second player's client.
/// </summary>
public class SecondLoginTests
{
    private const int OTHER_PLAYER = 2;

    [Fact]
    public async Task ASecondTicketOnALoggedInConnection_IsIgnored()
    {
        var harness = new PacketHarness();

        // A valid ticket for another player, who is not banned and is let in.
        foreach (
            var service in new[]
            {
                typeof(IAuthenticationService),
                typeof(IHotelAvailability),
                typeof(ISanctionService),
                typeof(ISessionGateway),
            }
        )
            harness.Resolver.Overrides[service] = harness.Fakes.Create(service);

        harness.Fakes.Handlers["GetPlayerIdFromTicketAsync"] = _ => Task.FromResult(OTHER_PLAYER);
        harness.Fakes.Handlers["AdmitsAsync"] = _ => Task.FromResult(true);

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("SSOTicketMessageEvent"),
            PacketHarness.Payload(w => w.String("another-ticket").Int(0)),
            playerId: 1
        );

        replies.Should().BeEmpty();
        harness.Sent.OfType<AuthenticationOKMessage>().Should().BeEmpty();
        harness.Fakes.Log.Calls.Should().NotContain(c => c.Method == "AddSessionToPlayerAsync");
    }

    [Fact]
    public async Task AConnectionBoundToOnePlayer_IsNotRebound_AndItsPlayerLeavesWithIt()
    {
        var hotel = new SessionHarness();
        var session = hotel.NewSession(1);
        var key = session.SessionKey;

        await hotel.Gateway.AddSessionAsync(key, session);
        await hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);
        await hotel.Gateway.AddSessionToPlayerAsync(key, OTHER_PLAYER);

        hotel.Gateway.GetPlayerId(key).Value.Should().Be(SessionHarness.PlayerId);
        hotel
            .Gateway.GetOnlinePlayerIds()
            .Select(x => x.Value)
            .Should()
            .Equal(SessionHarness.PlayerId);

        await hotel.Gateway.RemoveSessionAsync(key, CancellationToken.None);

        hotel.Gateway.GetOnlinePlayerIds().Should().BeEmpty();
    }
}
