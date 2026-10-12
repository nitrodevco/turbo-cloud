using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// Habbo's wired whispers (the triggerer's hidden message echoed back to them, Show Message's
/// whisper) end with the receiver's room index and the bubble width, -1 when the box sets none
/// (official client capture, WIN63-202610121709, Whisper 35 B:
/// <c>00000000 0005 68656C6C6F 00000000 00000000 00000000 FFFFFFFF 00000000 FFFFFFFF</c>).
/// Ours stopped after the receiver.
/// </summary>
public class WiredWhisperTailTests
{
    [Fact]
    public void A_whisper_to_one_player_ends_with_the_receiver_and_no_width()
    {
        var packet = PacketHarness.Encode(
            new WhisperMessageComposer
            {
                ObjectId = 0,
                Text = "hello",
                Gesture = AvatarGestureType.None,
                StyleId = 0,
                Links = [],
                TrackingId = -1,
                ReceiverRoomIndex = 0,
            }
        );

        Convert
            .ToHexString(packet.PopBytes(packet.Remaining))
            .Should()
            .Be("00000000000568656C6C6F000000000000000000000000FFFFFFFF00000000FFFFFFFF");
    }
}
