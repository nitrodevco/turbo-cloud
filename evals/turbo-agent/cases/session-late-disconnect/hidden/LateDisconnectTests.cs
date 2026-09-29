using EvalHarness;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: when a player reconnects, the old connection's socket can close
/// after the new one has logged in. That late disconnect must not unregister the new one.
/// Real SessionGateway + real PlayerPresenceGrain; only the Orleans boundary is faked.
/// </summary>
public class LateDisconnectTests
{
    private static async Task<(SessionHarness h, Turbo.Primitives.Networking.SessionKey a, Turbo.Primitives.Networking.SessionKey b)> Reconnect()
    {
        var h = new SessionHarness();
        var a = h.NewSession(1);
        var b = h.NewSession(2);
        await h.Gateway.AddSessionAsync(a.SessionKey, a);
        await h.Gateway.AddSessionToPlayerAsync(a.SessionKey, SessionHarness.PlayerId);
        await h.Gateway.AddSessionAsync(b.SessionKey, b);
        await h.Gateway.AddSessionToPlayerAsync(b.SessionKey, SessionHarness.PlayerId);
        await SessionHarness.Settle();
        return (h, a.SessionKey, b.SessionKey);
    }

    [Fact]
    public async Task LateDisconnectOfOldConnection_KeepsReplacementRegistered()
    {
        var (h, a, b) = await Reconnect();

        await h.Gateway.RemoveSessionAsync(a, CancellationToken.None);
        await SessionHarness.Settle();

        Assert.True(await h.PresenceGrain.HasActiveSessionAsync(CancellationToken.None));
        Assert.Same(h.Gateway.GetSessionObserver(b), SessionAsserts.PresenceObserver(h.Presence));
        Assert.Equal(SessionHarness.PlayerId, (int)h.Gateway.GetPlayerId(b));
    }

    [Fact]
    public async Task AfterLateDisconnect_ComposersStillReachReplacement()
    {
        var (h, a, b) = await Reconnect();
        await h.Gateway.RemoveSessionAsync(a, CancellationToken.None);
        await SessionHarness.Settle();

        var composer = SessionAsserts.AnyComposer<PingMessage>();
        await h.PresenceGrain.SendComposerAsync(composer, CancellationToken.None);
        await SessionHarness.Settle();

        Assert.Contains(composer, h.Received(b));
        Assert.DoesNotContain(composer, h.Received(a));
    }

    [Fact]
    public async Task DisconnectOfOnlyConnection_Unregisters()
    {
        var h = new SessionHarness();
        var a = h.NewSession(1);
        await h.Gateway.AddSessionAsync(a.SessionKey, a);
        await h.Gateway.AddSessionToPlayerAsync(a.SessionKey, SessionHarness.PlayerId);
        await SessionHarness.Settle();

        await h.Gateway.RemoveSessionAsync(a.SessionKey, CancellationToken.None);
        await SessionHarness.Settle();

        Assert.False(await h.PresenceGrain.HasActiveSessionAsync(CancellationToken.None));
        Assert.Equal(-1, (int)h.Gateway.GetPlayerId(a.SessionKey));
    }

    [Fact]
    public async Task ReplacementsOwnDisconnect_StillUnregisters()
    {
        var (h, a, b) = await Reconnect();
        await h.Gateway.RemoveSessionAsync(a, CancellationToken.None);
        await h.Gateway.RemoveSessionAsync(b, CancellationToken.None);
        await SessionHarness.Settle();

        Assert.False(await h.PresenceGrain.HasActiveSessionAsync(CancellationToken.None));
    }
}
