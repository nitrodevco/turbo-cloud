using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// What an operator reaches a connection through: who is online, and sending a player out with a
/// last word. The real gateway, with a fake socket.
/// </summary>
public class SessionGatewayDisconnectTests
{
    private readonly SessionHarness _hotel = new();

    private async Task<Turbo.Primitives.Networking.SessionKey> ConnectAsync(int n, bool loggedIn)
    {
        var session = _hotel.NewSession(n);
        var key = session.SessionKey;

        await _hotel.Gateway.AddSessionAsync(key, session);

        if (loggedIn)
            await _hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);

        return key;
    }

    [Fact]
    public async Task OnlinePlayers_AreThoseWithALoggedInConnection_NotEveryOpenSocket()
    {
        _hotel.Gateway.GetOnlinePlayerIds().Should().BeEmpty();

        await ConnectAsync(2, loggedIn: false);
        _hotel.Gateway.GetOnlinePlayerIds().Should().BeEmpty();

        await ConnectAsync(1, loggedIn: true);

        _hotel
            .Gateway.GetOnlinePlayerIds()
            .Select(x => x.Value)
            .Should()
            .Equal(SessionHarness.PlayerId);
    }

    [Fact]
    public async Task Disconnect_SendsTheLastWord_ThenClosesTheConnection()
    {
        var key = await ConnectAsync(1, loggedIn: true);
        var farewell = new HabboBroadcastMessageComposer { Message = "Goodbye" };

        var sent = await _hotel.Gateway.DisconnectPlayerAsync(
            SessionHarness.PlayerId,
            farewell,
            CancellationToken.None
        );

        sent.Should().BeTrue();
        _hotel.Received(key).Should().Contain(farewell);
        _hotel.WasClosed(key).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnect_NeedsNoLastWord()
    {
        var key = await ConnectAsync(1, loggedIn: true);

        (
            await _hotel.Gateway.DisconnectPlayerAsync(
                SessionHarness.PlayerId,
                null,
                CancellationToken.None
            )
        )
            .Should()
            .BeTrue();

        _hotel.WasClosed(key).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnect_OfAPlayerWithNoConnection_DoesNothing_AndSaysSo()
    {
        var key = await ConnectAsync(2, loggedIn: false);

        (await _hotel.Gateway.DisconnectPlayerAsync(77, null, CancellationToken.None))
            .Should()
            .BeFalse();

        _hotel.WasClosed(key).Should().BeFalse();
    }
}
