using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room configuration flags are five bytes on the wire, as Habbo sends them: the four the
/// client reads (_-B4.as:50-65, WIN63-202610131318) and a fifth it does not, zero in all 15
/// official samples (official client capture 2026-10-12/13, own room and group room). Turbo sent
/// four.
/// </summary>
public sealed class ConfigurationItemStatesPacketTests
{
    [Fact]
    public void The_flags_are_followed_by_a_fifth_zero_byte()
    {
        var packet = PacketHarness.Encode(
            new ConfigurationItemStatesMessageComposer
            {
                IsHanditemControlBlocked = true,
                ChooserDisabled = false,
                FreeFurniMovementsEnabled = true,
                InvisibleFurni = true,
            }
        );

        packet.PopBoolean().Should().BeTrue();
        packet.PopBoolean().Should().BeFalse();
        packet.PopBoolean().Should().BeTrue();
        packet.PopBoolean().Should().BeTrue();
        packet.PopByte().Should().Be(0);
        packet.Remaining.Should().Be(0);
    }
}
