using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Can Perform Movements" (wf_cnd_valid_moves) checks the moves of the stack's effects before any
/// of them is made: in a random order of its own, each from where the moves before it leave the
/// furni, with rolls of its own. A click runs the stack; a switch the stack toggles shows whether
/// its effects ran.
/// </summary>
public sealed class WiredValidMovesTests
{
    private const int TRIGGER = 1;
    private const int CONDITION = 2;
    private const int TOGGLE = 3;
    private const int FIRST_MOVE = 4;

    private const int CLICK_ME = 20;
    private const int LAMP = 21;
    private const int SWITCH = 22;

    // The editor's "move west" arrow and its "move randomly" option.
    private const int MOVE_WEST = 7;
    private const int MOVE_RANDOM = 1;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredValidMovesTests()
    {
        _room.Enter(5, 6, 6);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(SWITCH, 7, 1);
    }

    // The user's setup: two effects move the same furni. From where it stands each move fits;
    // from where the first leaves it, the second would leave the room.
    [Fact]
    public async Task A_second_move_of_one_furni_is_checked_from_where_the_first_leaves_it()
    {
        _room.AddFloorItem(LAMP, 1, 3);
        await BuildAsync(MOVE_WEST, MOVE_WEST);

        await ClickAsync();

        (_room.FloorItem(LAMP).X, _room.FloorItem(LAMP).Y).Should().Be((1, 3));
        SwitchState().Should().Be(0, "a move that cannot be made stops the whole stack");
    }

    [Fact]
    public async Task Moves_that_can_all_be_made_run()
    {
        _room.AddFloorItem(LAMP, 2, 3);
        await BuildAsync(MOVE_WEST, MOVE_WEST);

        await ClickAsync();

        (_room.FloorItem(LAMP).X, _room.FloorItem(LAMP).Y).Should().Be((0, 3));
        SwitchState().Should().Be(1);
    }

    // In the corner a random step is blocked half the time. The check rolls its own step and the
    // effect rolls again when it runs, as the effects draw their order again after the check
    // (official client capture, WIN63-202610201032: 13 of 24 passes then ran a blocked order;
    // that the rolls are drawn again likewise is inferred from it). A failed check moves nothing.
    [Fact]
    public async Task A_random_move_is_rolled_again_after_the_check()
    {
        _room.AddFloorItem(LAMP, 0, 7);
        await BuildAsync(MOVE_RANDOM);

        var movedAfterAPass = new HashSet<bool>();

        for (var i = 0; i < 60; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(LAMP), _room.Map.ToIdx(0, 7));

            var stateBefore = SwitchState();

            await ClickAsync();

            var lamp = _room.FloorItem(LAMP);
            var moved = (lamp.X, lamp.Y) != (0, 7);

            if (SwitchState() == stateBefore)
                moved.Should().BeFalse($"click {i + 1} failed the check and moved nothing");
            else
                movedAfterAPass.Add(moved);
        }

        movedAfterAPass.Should().BeEquivalentTo([true, false]);
    }

    private int SwitchState() => _room.FloorItem(SWITCH).Logic.GetState();

    private async Task BuildAsync(params int[] movements)
    {
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 5, 0, "wf_trg_click_furni");
        _room.AddBox<WiredConditionValidMoves>(CONDITION, 5, 0, "wf_cnd_valid_moves");
        _room.AddBox<WiredActionToggleItemState>(TOGGLE, 5, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (await _room.SaveAsync<UpdateConditionMessage>(CONDITION, definitionSpecifics: [0]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                TOGGLE,
                intParams: [0],
                stuffIds: [SWITCH],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var boxId = FIRST_MOVE;

        foreach (var movement in movements)
        {
            _room.AddBox<WiredActionMoveRotateFurni>(boxId, 5, 0, "wf_act_move_rotate");

            (
                await _room.SaveAsync<UpdateActionMessage>(
                    boxId,
                    intParams: [movement, 0],
                    stuffIds: [LAMP],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();

            boxId++;
        }

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(5, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);
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
