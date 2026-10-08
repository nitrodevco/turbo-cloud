using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Set furni altitude" with its operator as the editor lists it (<c>wiredfurni.params.operator.0</c>
/// Increase, <c>.1</c> Decrease, <c>.2</c> Set value; Adjust Clock shares the radio). A click on a
/// furni runs it twice with 1.50 on a furni standing at 0.
/// </summary>
public sealed class WiredSetAltitudeOperatorTests
{
    private const int CLICK_ME = 23;
    private const int LIFTED = 24;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSetAltitudeOperatorTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LIFTED, 5, 5);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(1, 0)]
    [InlineData(2, 150)]
    public async Task The_operator_raises_lowers_or_sets_the_altitude(int op, int expected)
    {
        await BuildAsync(op);

        for (var i = 0; i < 2; i++)
        {
            await _room
                .FloorItem(CLICK_ME)
                .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
            await TickAsync(4);
        }

        _room.FloorItem(LIFTED).Z.ToInt().Should().Be(expected);
    }

    private async Task BuildAsync(int op)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionSetAltitude>(2, 0, 0, "wf_act_set_altitude");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [150, op],
                stuffIds: [LIFTED],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
