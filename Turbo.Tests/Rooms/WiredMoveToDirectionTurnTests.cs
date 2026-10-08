using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Move furni to direction" with the turn its editor saves (AS3 <c>MoveToDirection</c>,
/// <c>wiredfurni.params.turn.0</c> to <c>.6</c>: wait, right 45, right 90, left 45, left 90, back,
/// random). The furni stands on the room's east edge heading east, so it is blocked and turns.
/// </summary>
public sealed class WiredMoveToDirectionTurnTests
{
    private const int CLICK_ME = 23;
    private const int MOVED = 24;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredMoveToDirectionTurnTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(MOVED, 7, 3);
    }

    [Theory]
    [InlineData(0, 7, 3)]
    [InlineData(1, 7, 4)]
    [InlineData(2, 7, 4)]
    [InlineData(3, 7, 2)]
    [InlineData(4, 7, 2)]
    [InlineData(5, 6, 3)]
    public async Task A_blocked_furni_turns_the_way_the_editor_says(int turn, int x, int y)
    {
        await BuildAsync(turn);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);

        var moved = _room.FloorItem(MOVED);

        (moved.X, moved.Y).Should().Be((x, y));
    }

    private async Task BuildAsync(int turn)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveToDirection>(2, 0, 0, "wf_act_move_to_dir");

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
                intParams: [(int)Rotation.East, turn, 0],
                stuffIds: [MOVED],
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
