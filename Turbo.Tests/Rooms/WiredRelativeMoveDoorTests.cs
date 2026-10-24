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
/// Move Furni (relative) may put a furni on the room's door tile: Habbo moved a block from 5,4
/// onto the door at 4,4, and the next move off the map from there sent nothing (official client
/// capture 2026-10-24T07-44-36, WIN63-202610201032, 1/1). The harness room's door is at 0,0.
/// </summary>
public sealed class WiredRelativeMoveDoorTests
{
    private const int CLICK_ME = 20;
    private const int BLOCK = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // action.33.B5
    [Fact]
    public async Task Action33B5_AFurniMovesOntoTheDoorTileAndNoFurther()
    {
        _room.AddFloorItem(CLICK_ME, 6, 6);
        _room.AddFloorItem(BLOCK, 1, 0);
        _room.AddBox<WiredTriggerClickFurni>(1, 7, 7, "wf_trg_click_furni");
        _room.AddBox<WiredActionRelativeMove>(2, 7, 7, "wf_act_rel_mov");

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
                intParams: [-1, 0],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(7, 7)],
            },
            Ct
        );
        await TickAsync(1);

        await ClickAsync();
        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y).Should().Be((0, 0));

        await ClickAsync();
        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y).Should().Be((0, 0));
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
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
