using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>WiredMenuError</c> as Flash reads it (<c>WiredMenuErrorMessageParser.parse</c>:
/// <c>readShort</c>, WIN63-202610151745), and when Habbo sends it: the inspection tab polls
/// <c>WiredGetVariablesForObject</c> every 500 ms, and a furni picked up while inspected is
/// answered with error 0 on every poll, no bubble (official client capture, WIN63-202610201032,
/// 29 of 29 polls in 2 runs). Ours answered nothing.
/// </summary>
public sealed class WiredMenuErrorTests
{
    private const int LAMP = 20;
    private const int PICKED_UP = 21;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_menu_error_carries_its_code_as_a_short()
    {
        var packet = PacketHarness.Encode(new WiredMenuErrorEventMessageComposer { ErrorCode = 7 });

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredMenuErrorMessageComposer"));
        packet.PopShort().Should().Be(7);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task The_room_says_when_an_inspected_furni_is_no_longer_there()
    {
        var room = new WiredRoom(8, 8);

        room.AddFloorItem(LAMP, 2, 3);

        var here = await room.Harness.Room.GetAllVariablesForBindingAsync(
            ActionContext.CreateForSystem(1),
            new WiredVariableBinding(WiredVariableTargetType.Furni, LAMP),
            Ct
        );
        var gone = await room.Harness.Room.GetAllVariablesForBindingAsync(
            ActionContext.CreateForSystem(1),
            new WiredVariableBinding(WiredVariableTargetType.Furni, PICKED_UP),
            Ct
        );

        here!.TargetPresent.Should().BeTrue();
        gone!.TargetPresent.Should().BeFalse();
        gone.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task A_poll_for_a_furni_that_left_the_room_is_answered_with_menu_error_0()
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers["GetAllVariablesForBindingAsync"] = _ =>
            Task.FromResult<WiredObjectVariablesSnapshot?>(
                new WiredObjectVariablesSnapshot { TargetPresent = false, Values = [] }
            );

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("WiredGetVariablesForObjectMessageEvent"),
            PacketHarness.Payload(w => w.Int((int)WiredVariableTargetType.Furni).Int(PICKED_UP)),
            playerId: 1,
            roomId: 1
        );

        replies.Should().ContainSingle();
        replies[0].Header.Should().Be(PacketHarness.Outgoing("WiredMenuErrorMessageComposer"));
        replies[0].PopShort().Should().Be(0);
        replies[0].Remaining.Should().Be(0);
    }
}
