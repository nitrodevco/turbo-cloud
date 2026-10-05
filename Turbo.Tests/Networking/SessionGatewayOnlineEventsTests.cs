using FluentAssertions;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Events;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// The gateway announces a player coming online and going, for whatever watches the hotel (the
/// admin panel's live updates). A login that replaces the player's earlier connection, and that
/// earlier connection closing, leave them online, so neither says anything.
/// </summary>
public class SessionGatewayOnlineEventsTests
{
    private readonly SessionHarness _hotel = new();

    public SessionGatewayOnlineEventsTests() => _hotel.Events.Record<PlayerOnlineChangedEvent>();

    private async Task<SessionKey> LogInAsync(int n)
    {
        var session = _hotel.NewSession(n);

        await _hotel.Gateway.AddSessionAsync(session.SessionKey, session);
        await _hotel.Gateway.AddSessionToPlayerAsync(session.SessionKey, SessionHarness.PlayerId);

        return session.SessionKey;
    }

    private async Task<bool[]> AnnouncedAsync()
    {
        await SessionHarness.Settle();

        return
        [
            .. _hotel
                .Events.Of<PlayerOnlineChangedEvent>()
                .Where(x => x.PlayerId.Value == SessionHarness.PlayerId)
                .Select(x => x.Online),
        ];
    }

    [Fact]
    public async Task LoggingIn_AndTheConnectionClosing_AreEachAnnouncedOnce()
    {
        var key = await LogInAsync(1);

        (await AnnouncedAsync()).Should().Equal(true);

        await _hotel.Gateway.RemoveSessionAsync(key, CancellationToken.None);

        (await AnnouncedAsync()).Should().Equal(true, false);
    }

    [Fact]
    public async Task ALoginReplacingAnother_LeavesThePlayerOnline_SoNothingIsAnnounced()
    {
        var first = await LogInAsync(1);
        var second = await LogInAsync(2);

        await _hotel.Gateway.RemoveSessionAsync(first, CancellationToken.None);

        (await AnnouncedAsync()).Should().Equal([true], "the player never went offline");

        await _hotel.Gateway.RemoveSessionAsync(second, CancellationToken.None);

        (await AnnouncedAsync()).Should().Equal(true, false);
    }

    [Fact]
    public async Task TheSessionLeavingThePlayer_IsAnnounced()
    {
        await LogInAsync(1);

        await _hotel.Gateway.RemoveSessionFromPlayerAsync(
            SessionHarness.PlayerId,
            CancellationToken.None
        );

        (await AnnouncedAsync()).Should().Equal(true, false);
    }
}
