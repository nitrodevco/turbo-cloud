using FluentAssertions;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Orleans;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A plugin's handler asks whether the session its packet came on accepted the plugin's
/// capability (docs/plugin-packets.md): a client may send an extension packet either way.
/// </summary>
public class SessionCapabilityQueryTests
{
    private const string CAPABILITY = "test.plugin";

    private readonly SessionHarness _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSessionThatAcceptedIt_IsTold_AtItsVersion()
    {
        var key = await LogInAsync(1);

        await _hotel.PresenceGrain.SetClientCapabilitiesAsync([Cap(CAPABILITY)], Ct);

        (await _hotel.PresenceGrain.GetClientCapabilityVersionAsync(key, CAPABILITY, Ct))
            .Should()
            .Be(1);
        (await AcceptsAsync(key, CAPABILITY)).Should().BeTrue();
        (await AcceptsAsync(key, "other.plugin")).Should().BeFalse();
    }

    [Fact]
    public async Task ASessionThatNeverAsked_IsNot()
    {
        var key = await LogInAsync(1);

        (await AcceptsAsync(key, CAPABILITY)).Should().BeFalse();
    }

    [Fact]
    public async Task AReplacedSession_IsNot_AndNeitherIsItsReplacementUntilItAsks()
    {
        var first = await LogInAsync(1);

        await _hotel.PresenceGrain.SetClientCapabilitiesAsync([Cap(CAPABILITY)], Ct);

        var second = await LogInAsync(2);

        (await AcceptsAsync(first, CAPABILITY)).Should().BeFalse();
        (await AcceptsAsync(second, CAPABILITY)).Should().BeFalse();

        await _hotel.PresenceGrain.SetClientCapabilitiesAsync([Cap(CAPABILITY)], Ct);

        (await AcceptsAsync(second, CAPABILITY)).Should().BeTrue();
        (await AcceptsAsync(first, CAPABILITY)).Should().BeFalse();
    }

    [Fact]
    public async Task ALeftSession_IsNot()
    {
        var key = await LogInAsync(1);

        await _hotel.PresenceGrain.SetClientCapabilitiesAsync([Cap(CAPABILITY)], Ct);
        await _hotel.Gateway.RemoveSessionAsync(key, Ct);

        (await AcceptsAsync(key, CAPABILITY)).Should().BeFalse();
    }

    private async Task<SessionKey> LogInAsync(int n)
    {
        var session = _hotel.NewSession(n);
        var key = session.SessionKey;

        await _hotel.Gateway.AddSessionAsync(key, session);
        await _hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);

        return key;
    }

    /// <summary>What a plugin's handler asks, with its message context's player and session.</summary>
    private Task<bool> AcceptsAsync(SessionKey key, string capability) =>
        _hotel
            .Fakes.Create<IGrainFactory>()
            .SessionAcceptsCapabilityAsync(SessionHarness.PlayerId, key, capability, Ct);

    private static ClientCapabilitySnapshot Cap(string name) => new() { Name = name, Version = 1 };
}
