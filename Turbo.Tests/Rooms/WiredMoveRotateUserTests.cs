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
/// W1 action.4.B1: Move and Rotate Furni into a user's tile is blocked, whatever its third int;
/// with nobody there it moves (official client capture 2026-10-24, W1 lab, 4/4 + 1 free move).
/// </summary>
public sealed class WiredMoveRotateUserTests
{
    private const int CLICK_ME = 23;
    private const int BLOCK = 20;
    private const int MOVE_EAST = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(0, 4, 3)]
    [InlineData(1, 4, 3)]
    [InlineData(0, 1, 1)]
    public async Task A_user_on_the_next_tile_blocks_the_move(int third, int userX, int userY)
    {
        _room.Enter(5, userX, userY);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(BLOCK, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveRotateFurni>(2, 0, 0, "wf_act_move_rotate");

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
                intParams: [MOVE_EAST, 0, third],
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
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y)
            .Should()
            .Be(userX == 4 ? (3, 3) : (4, 3));
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
