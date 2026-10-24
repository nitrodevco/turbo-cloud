using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A stack's effects run in a random order unless "Execute In Order" (wf_xtra_exec_in_order)
/// keeps them in stacking order. A lamp stands against the west wall; the stack moves it west,
/// then east. West first is blocked by the wall and leaves the lamp one tile out; east first
/// brings it back to the wall. "Can Perform Movements" checks a random order of its own, and the
/// room is told each furni's moves of a firing at once (official client capture,
/// WIN63-202610201032, two move and two rotate effects on one sofa, 84 firings).
/// </summary>
public sealed class WiredEffectOrderTests
{
    private const int CLICK_ME = 20;
    private const int LAMP = 21;
    private const int SWITCH = 22;

    // The editor's "move west" and "move east" arrows, and its "rotate clockwise" option.
    private const int MOVE_WEST = 7;
    private const int MOVE_EAST = 5;
    private const int NO_MOVE = 0;
    private const int ROTATE_CW = 1;

    private static readonly (int Move, int Rotate) West = (MOVE_WEST, 0);
    private static readonly (int Move, int Rotate) East = (MOVE_EAST, 0);
    private static readonly (int Move, int Rotate) Turn = (NO_MOVE, ROTATE_CW);

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredEffectOrderTests()
    {
        _room.Enter(5, 6, 6);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 0, 3);
        _room.AddFloorItem(SWITCH, 7, 1);
    }

    [Fact]
    public async Task Without_an_addon_the_effects_run_in_a_random_order()
    {
        await BuildAsync(inOrder: false, checkMoves: false, West, East);

        var endedAt = new HashSet<int>();

        for (var i = 0; i < 40; i++)
            endedAt.Add(await ClickFromAsync(0));

        endedAt.Should().BeEquivalentTo([0, 1]);
    }

    // action.0.X1: Toggle and Match Snapshot (state only, snapshot "0") on one lamp. The lamp
    // ended at the snapshot on every click, the snapshot box below or above the toggle (official
    // client capture 2026-10-24, W2 lab 6/6 + W1 lab 6/6 reversed). Ours ran the two in a random
    // order and left the lamp on about half the clicks.
    [Fact]
    public async Task Match_snapshot_runs_after_a_toggle_of_the_same_furni()
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMatchToSnapshot>(2, 0, 0, "wf_act_match_to_sshot");
        _room.AddBox<WiredActionToggleItemState>(3, 0, 0, "wf_act_toggle_state");
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

        foreach (var (box, ints) in new[] { (2, new[] { 1, 0, 0, 0, 0 }), (3, new[] { 0 }) })
        {
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: ints,
                    stuffIds: [LAMP],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();
        }

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

        var endedIn = new HashSet<int>();

        for (var i = 0; i < 20; i++)
        {
            await ClickFromAsync(0);
            endedIn.Add(_room.FloorItem(LAMP).Logic.GetState());
        }

        endedIn.Should().Equal(0);
    }

    [Fact]
    public async Task Execute_in_order_runs_them_in_stacking_order()
    {
        await BuildAsync(inOrder: true, checkMoves: false, West, East);

        for (var i = 0; i < 20; i++)
            (await ClickFromAsync(0))
                .Should()
                .Be(1, $"click {i + 1} moved west, against the wall, before east");
    }

    // Official: the condition passed 24 of 44 firings and 13 of those 24 then ran an order that
    // blocked. East then west is the only order it passes; the effects then run east then west
    // (back at the wall) or west then east (one tile out), and a failed check moves nothing.
    [Fact]
    public async Task Can_perform_movements_checks_an_order_of_its_own()
    {
        await BuildAsync(inOrder: false, checkMoves: true, West, East);

        var passedEndedAt = new HashSet<int>();
        var failed = 0;

        for (var i = 0; i < 60; i++)
        {
            var stateBefore = SwitchState();
            var endedAt = await ClickFromAsync(0);

            if (SwitchState() == stateBefore)
            {
                endedAt.Should().Be(0, $"click {i + 1} failed the check and moved nothing");
                failed++;
            }
            else
                passedEndedAt.Add(endedAt);
        }

        failed.Should().BeInRange(1, 59, "the check passes on half the orders it draws");
        passedEndedAt
            .Should()
            .BeEquivalentTo([0, 1], "the effects draw their order again after the check");
    }

    // Official: with Execute In Order the effects ran in stack order 15 of 15, yet the condition
    // still failed 25 of 40 firings, also when the stack order was valid. Here the stack order
    // (west, then east) is blocked: a check that followed it would never pass.
    [Fact]
    public async Task Can_perform_movements_ignores_execute_in_order()
    {
        await BuildAsync(inOrder: true, checkMoves: true, West, East);

        var passed = 0;

        for (var i = 0; i < 40; i++)
        {
            var stateBefore = SwitchState();
            var endedAt = await ClickFromAsync(0);

            if (SwitchState() == stateBefore)
            {
                endedAt.Should().Be(0, $"click {i + 1} failed the check and moved nothing");

                continue;
            }

            endedAt.Should().Be(1, $"click {i + 1} ran west, against the wall, then east");
            passed++;
        }

        passed.Should().BeInRange(1, 39);
    }

    // Official: without the add-on one WiredMovements per firing, from the start tile to the
    // final one, facing the final way, whatever order the effects ran in.
    [Fact]
    public async Task Without_an_addon_a_furni_moved_by_several_effects_moves_once()
    {
        await BuildAsync(inOrder: false, checkMoves: false, West, Turn, West);

        for (var i = 0; i < 10; i++)
        {
            var moves = await ClickAndRecordAsync(3);

            moves
                .Should()
                .ContainSingle($"click {i + 1} sent one movement")
                .Which.Should()
                .BeEquivalentTo(new[] { "21: 3,3 -> 1,3 r2" });
        }
    }

    // Official: with Execute In Order only the last step reaches the client, from the tile
    // before it.
    [Fact]
    public async Task Execute_in_order_sends_only_the_last_move()
    {
        await BuildAsync(inOrder: true, checkMoves: false, West, Turn, West);

        var updatesBefore = LampUpdates();
        var moves = await ClickAndRecordAsync(3);

        moves.Should().ContainSingle().Which.Should().BeEquivalentTo(new[] { "21: 2,3 -> 1,3 r2" });
        LampUpdates().Should().Be(updatesBefore);
    }

    // Official: with Execute In Order a last step that only turns the furni is a plain
    // ObjectUpdate, with no WiredMovements.
    [Fact]
    public async Task Execute_in_order_ending_on_a_turn_sends_an_object_update()
    {
        await BuildAsync(inOrder: true, checkMoves: false, West, West, Turn);

        var updatesBefore = LampUpdates();
        var moves = await ClickAndRecordAsync(3);

        moves.Should().BeEmpty();
        LampUpdates().Should().Be(updatesBefore + 1);

        var update = _room
            .Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>()
            .Last(x => x.FloorItem.ObjectId == LAMP)
            .FloorItem;

        (update.X, update.Y, update.Rotation).Should().Be((1, 3, Rotation.East));
    }

    // Official (2026-10-22T22-02-10): one rotate effect, no add-on, turned the sofa with a plain
    // ObjectUpdate 6 of 6, never a WiredMovements, and two rotates the random add-on picked 1 of 1.
    [Fact]
    public async Task Without_an_addon_a_furni_only_turned_gets_an_object_update()
    {
        await BuildAsync(inOrder: false, checkMoves: false, Turn, West, East);

        for (var i = 0; i < 10; i++)
        {
            var updatesBefore = LampUpdates();

            (await ClickAndRecordAsync(3))
                .Should()
                .BeEmpty($"click {i + 1} left the lamp on its tile");
            LampUpdates().Should().Be(updatesBefore + 1);
        }
    }

    // Official (2026-10-22T22-02-10, same ms each time): the turn's ObjectUpdate came before the
    // boxes' flash (ObjectsDataUpdate) 6 of 6, a WiredMovements after it 16 of 16.
    [Fact]
    public async Task A_turn_goes_before_the_boxes_flash()
    {
        await BuildAsync(inOrder: false, checkMoves: false, Turn);

        (await SentForClickAsync(3)).Should().Equal("ObjectUpdate", "ObjectsDataUpdate");
    }

    [Fact]
    public async Task A_move_goes_after_the_boxes_flash()
    {
        await BuildAsync(inOrder: false, checkMoves: false, West, Turn);

        (await SentForClickAsync(3)).Should().Equal("ObjectsDataUpdate", "WiredMovements");
    }

    private int SwitchState() => _room.FloorItem(SWITCH).Logic.GetState();

    /// <summary>What the room was sent about the lamp and the boxes for one click, in order.</summary>
    private async Task<List<string>> SentForClickAsync(int fromX)
    {
        var before = RoomSent().Count;

        await ClickFromAsync(fromX);

        return
        [
            .. RoomSent()
                .Skip(before)
                .Select(x =>
                    x switch
                    {
                        ObjectUpdateMessageComposer => "ObjectUpdate",
                        ObjectsDataUpdateMessageComposer => "ObjectsDataUpdate",
                        WiredMovementsMessageComposer => "WiredMovements",
                        _ => null,
                    }
                )
                .OfType<string>(),
        ];
    }

    private List<IComposer> RoomSent() => [.. _room.Harness.Fakes.Sent(SendRoute.Room)];

    private int LampUpdates() =>
        _room
            .Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>()
            .Count(x => x.FloorItem.ObjectId == LAMP);

    private List<WiredFloorItemMovementSnapshot[]> FloorMovements() =>
        [
            .. _room
                .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
                .Where(x => x.FloorItems.Count > 0)
                .Select(x => x.FloorItems.ToArray()),
        ];

    /// <summary>Each WiredMovements a click from x = <paramref name="fromX"/> sent, as text.</summary>
    private async Task<List<string[]>> ClickAndRecordAsync(int fromX)
    {
        var before = FloorMovements().Count;

        await ClickFromAsync(fromX);

        return
        [
            .. FloorMovements()
                .Skip(before)
                .Select(x =>
                    x.Select(m =>
                            $"{(int)m.ObjectId}: {m.SourceX},{m.SourceY} -> {m.TargetX},{m.TargetY} r{(int)m.Rotation}"
                        )
                        .ToArray()
                ),
        ];
    }

    private async Task<int> ClickFromAsync(int x)
    {
        var lamp = _room.FloorItem(LAMP);

        _room.Map.MoveFloorItem(lamp, _room.Map.ToIdx(x, 3));
        lamp.SetRotation(Rotation.North);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return _room.FloorItem(LAMP).X;
    }

    private async Task BuildAsync(
        bool inOrder,
        bool checkMoves,
        params (int Move, int Rotate)[] effects
    )
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");

        if (inOrder)
            _room.AddBox<WiredAddonExecuteInOrder>(2, 0, 0, "wf_xtra_exec_in_order");

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

        if (checkMoves)
        {
            _room.AddBox<WiredConditionValidMoves>(9, 0, 0, "wf_cnd_valid_moves");
            _room.AddBox<WiredActionToggleItemState>(10, 0, 0, "wf_act_toggle_state");

            (await _room.SaveAsync<UpdateConditionMessage>(9, definitionSpecifics: [0]))
                .Should()
                .BeTrue();
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    10,
                    intParams: [0],
                    stuffIds: [SWITCH],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();
        }

        var box = 3;

        foreach (var (move, rotate) in effects)
        {
            _room.AddBox<WiredActionMoveRotateFurni>(box, 0, 0, "wf_act_move_rotate");

            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [move, rotate],
                    stuffIds: [LAMP],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();

            box++;
        }

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
