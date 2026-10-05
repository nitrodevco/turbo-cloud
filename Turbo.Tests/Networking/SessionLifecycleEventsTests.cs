using FluentAssertions;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Events;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>What a plugin sees of players coming and going, through the real gateway and event pipeline.</summary>
public class SessionLifecycleEventsTests
{
    private readonly SessionHarness _hotel = new();

    public SessionLifecycleEventsTests()
    {
        _hotel.Events.Record<PlayerConnectedEvent>();
        _hotel.Events.Record<PlayerDisconnectedEvent>();
    }

    private async Task<SessionKey> ConnectAsync(int n, bool loggedIn = true)
    {
        var session = _hotel.NewSession(n);

        await _hotel.Gateway.AddSessionAsync(session.SessionKey, session);

        if (loggedIn)
            await _hotel.Gateway.AddSessionToPlayerAsync(
                session.SessionKey,
                SessionHarness.PlayerId
            );

        return session.SessionKey;
    }

    private Task RemoveAsync(SessionKey key) =>
        _hotel.Gateway.RemoveSessionAsync(key, CancellationToken.None);

    [Fact]
    public async Task Login_PublishesConnectedOnce()
    {
        var key = await ConnectAsync(1);

        _hotel
            .Events.Of<PlayerConnectedEvent>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new PlayerConnectedEvent(SessionHarness.PlayerId, key));
    }

    [Fact]
    public async Task Removal_PublishesDisconnectedOnce()
    {
        var key = await ConnectAsync(1);

        await RemoveAsync(key);

        _hotel
            .Events.Of<PlayerDisconnectedEvent>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new PlayerDisconnectedEvent(SessionHarness.PlayerId, key));
    }

    [Fact]
    public async Task RemovedTwice_AcrossBothRemovalPaths_PublishesOnce()
    {
        var key = await ConnectAsync(1);

        await _hotel.Gateway.RemoveSessionFromPlayerAsync(
            SessionHarness.PlayerId,
            CancellationToken.None
        );
        await RemoveAsync(key);
        await RemoveAsync(key);

        _hotel.Events.Of<PlayerDisconnectedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SessionThatNeverLoggedIn_PublishesNothing()
    {
        var key = await ConnectAsync(1, loggedIn: false);

        await RemoveAsync(key);

        _hotel.Events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ThrowingHandlers_DoNotBreakLoginOrTeardown()
    {
        _hotel.Events.On<PlayerConnectedEvent>(_ => throw new InvalidOperationException("boom"));
        _hotel.Events.On<PlayerDisconnectedEvent>(_ => throw new InvalidOperationException("boom"));

        var key = await ConnectAsync(1);
        _hotel.Gateway.GetOnlinePlayerIds().Should().ContainSingle();

        await RemoveAsync(key);

        _hotel.Gateway.GetSession(key).Should().BeNull();
        _hotel.Gateway.GetOnlinePlayerIds().Should().BeEmpty();
    }
}
