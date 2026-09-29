using EvalHarness;
using Turbo.Primitives.Networking;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: a second login for the same player replaces the first. The old
/// connection is told why (DisconnectReason, concurrent login = 2, as the client reads it),
/// closed, and no longer acts as the player. Checked on the wire via the revision serializer.
/// </summary>
public class DuplicateLoginTests
{
    private const int ConcurrentLogin = 2;

    private static async Task<(SessionHarness h, SessionKey a, SessionKey b)> SecondLogin()
    {
        var h = new SessionHarness();
        var a = h.NewSession(1);
        var b = h.NewSession(2);
        await h.Gateway.AddSessionAsync(a.SessionKey, a);
        await h.Gateway.AddSessionToPlayerAsync(a.SessionKey, SessionHarness.PlayerId);
        await SessionHarness.Settle();
        await h.Gateway.AddSessionAsync(b.SessionKey, b);
        await h.Gateway.AddSessionToPlayerAsync(b.SessionKey, SessionHarness.PlayerId);
        await SessionHarness.Settle();
        return (h, a.SessionKey, b.SessionKey);
    }

    private static List<int?> DisconnectReasons(SessionHarness h, SessionKey key)
    {
        var header = PacketHarness.Outgoing("DisconnectReasonMessageComposer");
        var reasons = new List<int?>();
        foreach (var c in h.Received(key))
        {
            var p = PacketHarness.Encode(c);
            if (p.Header != header)
                continue;
            reasons.Add(p.Remaining >= 4 ? p.PopInt() : null);
        }
        return reasons;
    }

    [Fact]
    public async Task OldConnection_IsToldConcurrentLogin()
    {
        var (h, a, _) = await SecondLogin();
        Assert.Contains(ConcurrentLogin, DisconnectReasons(h, a));
    }

    [Fact]
    public async Task OldConnection_IsClosed()
    {
        var (h, a, b) = await SecondLogin();
        Assert.True(h.WasClosed(a));
        Assert.False(h.WasClosed(b));
    }

    [Fact]
    public async Task OldConnection_NoLongerActsAsThePlayer()
    {
        var (h, a, b) = await SecondLogin();
        Assert.Equal(-1, (int)h.Gateway.GetPlayerId(a));
        Assert.Equal(SessionHarness.PlayerId, (int)h.Gateway.GetPlayerId(b));
    }

    [Fact]
    public async Task NewConnection_IsNotToldToLeave()
    {
        var (h, _, b) = await SecondLogin();
        Assert.Empty(DisconnectReasons(h, b));
        Assert.Same(h.Gateway.GetSessionObserver(b), SessionAsserts.PresenceObserver(h.Presence));
    }

    [Fact]
    public async Task FirstLogin_KicksNobody()
    {
        var h = new SessionHarness();
        var a = h.NewSession(1);
        await h.Gateway.AddSessionAsync(a.SessionKey, a);
        await h.Gateway.AddSessionToPlayerAsync(a.SessionKey, SessionHarness.PlayerId);
        await SessionHarness.Settle();

        Assert.Empty(DisconnectReasons(h, a.SessionKey));
        Assert.False(h.WasClosed(a.SessionKey));
        Assert.Equal(SessionHarness.PlayerId, (int)h.Gateway.GetPlayerId(a.SessionKey));
    }
}
