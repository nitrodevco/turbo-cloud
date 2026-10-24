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
/// "Move furni to direction" meeting a user, and its random turn (official client capture
/// 2026-10-24, W1 lab, own room): with "block furni movements" ticked the furni stays where it
/// is, without turning, whatever the turn rule (action.13.B5, 4/4); unticked, the user is in the
/// way like furni and the turn rule applies, the furni never entering the user's tile
/// (action.13.B6, 4/4); either way the collision trigger fires (trigger.11.R1, 7/7). A blocked
/// furni turning at random always moved to a free neighbour in the same firing (action.13.B3,
/// 8/8). The furni heads east from 3,3; the user stands on 4,3.
/// </summary>
public sealed class WiredMoveToDirectionUserTests
{
    private const int CLICK_ME = 23;
    private const int MOVED = 24;
    private const int SWITCH = 25;
    private const int USER = 5;

    private const int TURN_WAIT = 0;
    private const int TURN_BACK = 5;
    private const int TURN_RANDOM = 6;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private int _turn;
    private bool _blockOnUsers;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredMoveToDirectionUserTests()
    {
        _room.Enter(USER, 4, 3);
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(MOVED, 3, 3);
        _room.AddFloorItem(SWITCH, 7, 6);
    }

    // action.13.B5
    [Theory]
    [InlineData(TURN_WAIT)]
    [InlineData(TURN_BACK)]
    [InlineData(TURN_RANDOM)]
    public async Task Blocked_by_a_user_the_furni_stays_without_turning(int turn)
    {
        await BuildAsync(turn, blockOnUsers: true);

        await ClickAsync();

        Position().Should().Be((3, 3));
        CollisionFired().Should().BeTrue();
    }

    // action.13.B6
    [Fact]
    public async Task Not_blocked_by_users_the_furni_turns_away_from_the_user()
    {
        await BuildAsync(TURN_BACK, blockOnUsers: false);

        await ClickAsync();

        Position().Should().Be((2, 3));
        CollisionFired().Should().BeTrue();
    }

    // action.13.B6, turn "wait": the furni waits behind the user rather than entering its tile.
    [Fact]
    public async Task Not_blocked_by_users_the_furni_never_enters_the_users_tile()
    {
        await BuildAsync(TURN_WAIT, blockOnUsers: false);

        await ClickAsync();

        Position().Should().Be((3, 3));
        CollisionFired().Should().BeTrue();
    }

    // action.13.B3: east into the user, every other way free.
    [Fact]
    public async Task A_random_turn_always_finds_a_free_neighbour()
    {
        await BuildAsync(TURN_RANDOM, blockOnUsers: false);

        for (var i = 0; i < 20; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(MOVED), _room.Map.ToIdx(3, 3));
            await RebuildHeadingAsync();

            await ClickAsync();

            Position().Should().NotBe((3, 3), $"firing {i + 1} found a free neighbour");
            Position().Should().NotBe((4, 3));
        }
    }

    private (int, int) Position() => (_room.FloorItem(MOVED).X, _room.FloorItem(MOVED).Y);

    private bool CollisionFired() => _room.FloorItem(SWITCH).Logic.GetState() != 0;

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
    }

    /// <summary>
    /// Click furni -> move to direction (east, <paramref name="turn"/>), and a second stack:
    /// collision -> toggle the switch.
    /// </summary>
    private async Task BuildAsync(int turn, bool blockOnUsers)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveToDirection>(2, 0, 0, "wf_act_move_to_dir");
        _room.AddBox<WiredTriggerHabboCollision>(3, 1, 0, "wf_trg_collision");
        _room.AddBox<WiredActionToggleItemState>(4, 1, 0, "wf_act_toggle_state");

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
        _turn = turn;
        _blockOnUsers = blockOnUsers;
        (await SaveMoverAsync()).Should().BeTrue();
        (await _room.SaveAsync<UpdateTriggerMessage>(3)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                4,
                intParams: [0],
                stuffIds: [SWITCH],
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
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(1, 0)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private Task<bool> SaveMoverAsync() =>
        _room.SaveAsync<UpdateActionMessage>(
            2,
            intParams: [(int)Rotation.East, _turn, _blockOnUsers ? 1 : 0],
            stuffIds: [MOVED],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ]
        );

    /// <summary>Saving the box again starts it from its saved heading (W1 action.13.R1, 5/5).</summary>
    private async Task RebuildHeadingAsync()
    {
        (await SaveMoverAsync()).Should().BeTrue();

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
