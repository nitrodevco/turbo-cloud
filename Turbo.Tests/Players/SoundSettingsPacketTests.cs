using FluentAssertions;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// <c>SetSoundSettings</c> as the client writes it (WIN63-202610201032
/// <c>sound/HabboSoundManagerFlash10.as:456</c> builds the composer from trax, furni, generic and
/// <c>outgoing/preferences/SetSoundSettingsComposer.as:13-15</c> pushes them back to front): the
/// system volume first, then furni, then trax - the order <c>AccountPreferencesParser</c> reads
/// them back in at login. Read the other way round, muting the system sounds muted trax.
/// </summary>
public sealed class SoundSettingsPacketTests
{
    [Fact]
    public async Task TheVolumesAreReadSystemFurniTrax()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("SetSoundSettingsMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).Int(20).Int(30)),
            playerId: 5
        );

        var call = harness
            .Fakes.Log.Of(nameof(IPlayerSettingsGrain.SetSoundSettingsAsync))
            .Should()
            .ContainSingle()
            .Subject;

        call.Args.Take(3).Should().Equal(10, 20, 30);
    }
}
