using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The packets the operator commands send, in the bytes the client reads: each is checked against
/// the field order of nitro-next's parser for it (<c>packages/nitro-packets/src/incoming</c>).
/// </summary>
public class OperatorComposerTests
{
    [Fact]
    public void ABroadcast_IsOneString_TheText()
    {
        var packet = PacketHarness.Encode(new HabboBroadcastMessageComposer { Message = "Hello" });

        packet.Header.Should().Be(PacketHarness.Outgoing("HabboBroadcastMessageComposer"));
        packet.PopString().Should().Be("Hello");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void AModeratorMessage_IsTheTextThenTheLink()
    {
        var packet = PacketHarness.Encode(
            new ModeratorMessageComposer { Message = "Calm down", Url = "event:navigator/goto/7" }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("ModeratorMessageComposer"));
        packet.PopString().Should().Be("Calm down");
        packet.PopString().Should().Be("event:navigator/goto/7");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void ABannedNotice_IsOneString_TheMessage()
    {
        var packet = PacketHarness.Encode(new UserBannedMessageComposer { Message = "Banned" });

        packet.Header.Should().Be(PacketHarness.Outgoing("UserBannedMessageComposer"));
        packet.PopString().Should().Be("Banned");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void AClosingNotice_IsTheMinutesLeft()
    {
        var packet = PacketHarness.Encode(
            new InfoHotelClosingMessageComposer { MinutesUntilClosing = 5 }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("InfoHotelClosingMessageComposer"));
        packet.PopInt().Should().Be(5);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void AMaintenanceNotice_IsWhetherItHasStarted_TheMinutesUntil_AndHowLongItLasts()
    {
        var packet = PacketHarness.Encode(
            new MaintenanceStatusMessageComposer
            {
                IsInMaintenance = false,
                MinutesUntilMaintenance = 10,
                DurationMinutes = 30,
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("MaintenanceStatusMessageComposer"));
        packet.PopBoolean().Should().BeFalse();
        packet.PopInt().Should().Be(10);
        packet.PopInt().Should().Be(30);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void AClosedNotice_IsTheOpeningTime_AndWhetherThePlayerWasThrownOut()
    {
        var packet = PacketHarness.Encode(
            new InfoHotelClosedMessageComposer
            {
                OpenHour = 18,
                OpenMinute = 30,
                UserThrownOutAtClose = true,
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("InfoHotelClosedMessageComposer"));
        packet.PopInt().Should().Be(18);
        packet.PopInt().Should().Be(30);
        packet.PopBoolean().Should().BeTrue();
        packet.End.Should().BeTrue();
    }
}
