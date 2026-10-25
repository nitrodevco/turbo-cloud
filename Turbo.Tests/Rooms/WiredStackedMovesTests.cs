using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Furni moved by wired where furni stand on furni, as Habbo moves them (official client capture
/// 2026-10-24, W1 lab, own room, "wlab5 golden2 asks"): a stackable block (height 1) with a
/// legotrophy on it (nothing stacks on a trophy, height 0), moved by Move and Rotate Furni one
/// tile north (action.4.X9/X9b, 18-40-50, 2/2 each), and a block stepped by Relative Furni
/// Movement through two stacks whose tops are 46 and 74 (action.4.X10, 18-41-43, 2/2 per mode).
/// </summary>
public sealed class WiredStackedMovesTests
{
    private const int CLICK_ME = 20;
    private const int BASE = 21;
    private const int TROPHY = 22;
    private const int MOVER = 30;
    private const int STACK_A = 31;
    private const int STACK_B = 32;
    private const int TRIGGER = 1;
    private const int ACTION = 2;
    private const int PHYSICS = 3;
    private const int MOVE_NORTH = 4;

    private readonly WiredRoom _room = new(12, 12);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    /// <summary>
    /// action.4.X9 / action.33.X9 / action.13.X9: the block and the trophy on it, both picked and
    /// moved one tile, are a coin flip every firing on Habbo: the trophy alone moves (dropped to
    /// the floor, the block blocked by it) or both move, the trophy on the block again, the block
    /// listed first; never the block alone (Move and Rotate both 3/6, Relative Move both 12/21,
    /// Move To Direction both 12/21, W1 lab wlab7 + wlab8, 2026-10-24/25). Ours moved the trophy
    /// alone every firing.
    /// </summary>
    [Theory]
    [InlineData("move_rotate")]
    [InlineData("rel_mov")]
    [InlineData("move_to_dir")]
    public async Task Picking_a_furni_and_the_one_on_it_moves_the_top_one_or_both_at_random_X9(
        string mover
    )
    {
        const string trophyAlone = "22 6,8 z1 -> 6,7 z0";
        const string both = "21 6,8 z0 -> 6,7 z0 | 22 6,8 z1 -> 6,7 z1";
        var outcomes = new List<string>();

        var block = _room.AddFloorItem(BASE, 6, 8);
        AddTrophy(6, 8, 1.0);
        await BuildMoverAsync(mover, [BASE, TROPHY]);

        for (var firing = 0; firing < 40; firing++)
        {
            _room.Map.MoveFloorItem(block, _room.Map.ToIdx(6, 8), Altitude.Zero);
            _room.Map.MoveFloorItem(
                _room.FloorItem(TROPHY),
                _room.Map.ToIdx(6, 8),
                Altitude.FromValue(1.0)
            );

            outcomes.Add(string.Join(" | ", await ClickAsync()));
        }

        outcomes.Should().OnlyContain(o => o == trophyAlone || o == both);
        outcomes.Should().Contain(trophyAlone).And.Contain(both);
    }

    /// <summary>
    /// action.4.X9b: the block picked alone moves without the trophy, which stays hanging where
    /// it was (no packet for it); the block is then refused under it again (cant_set_item).
    /// </summary>
    [Fact]
    public async Task Moving_the_base_leaves_the_furni_on_it_hanging_action_4_X9b()
    {
        _room.AddFloorItem(BASE, 6, 8);
        AddTrophy(6, 8, 1.0);
        await BuildMoveRotateAsync([BASE]);

        var moves = await ClickAsync();

        moves.Should().Equal("21 6,8 z0 -> 6,7 z0");

        var trophy = _room.FloorItem(TROPHY);

        (trophy.X, trophy.Y, trophy.Z.Value).Should().Be((6, 8, 1.0));
        _room
            .Harness.Module<RoomFurniModule>()
            .CanPlaceFloorItem(_room.FloorItem(BASE), 6, 8, Rotation.North)
            .Should()
            .BeFalse();
    }

    /// <summary>
    /// action.4.X10: without Movement Physics, or with keep altitude off, the block lands on top
    /// of each stack (46, then 74); with keep altitude it keeps its own altitude 0 inside the
    /// stacks, and only the step that leaves its tile changes a tile top: the step from stack to
    /// stack tells the client no height.
    /// </summary>
    [Theory]
    [InlineData(null, 46.0, 74.0, true)]
    [InlineData(0, 46.0, 74.0, true)]
    [InlineData(1, 0.0, 0.0, false)]
    public async Task A_block_stepped_through_stacks_lands_on_top_unless_it_keeps_altitude_action_4_X10(
        int? keepAltitude,
        double firstZ,
        double secondZ,
        bool secondStepSendsHeights
    )
    {
        _room.AddFloorItem(MOVER, 7, 8);
        Float(_room.AddFloorItem(STACK_A, 8, 8), 45);
        Float(_room.AddFloorItem(STACK_B, 9, 8), 73);
        await BuildRelativeMoveAsync(keepAltitude);
        _room.Harness.Room.TakeHeightMapUpdates();

        var first = await ClickAsync();
        var firstHeights = HeightsSent();
        var second = await ClickAsync();
        var secondHeights = HeightsSent();

        first.Should().Equal($"30 7,8 z0 -> 8,8 z{firstZ}");
        second.Should().Equal($"30 8,8 z{firstZ} -> 9,8 z{secondZ}");
        firstHeights.Should().Contain((7, 8));
        secondHeights.Any().Should().Be(secondStepSendsHeights);
    }

    private void Float(IRoomFloorItem item, double z) =>
        _room.Map.MoveFloorItem(item, _room.Map.ToIdx(item.X, item.Y), Altitude.FromValue(z));

    /// <summary>A legotrophy: height 0, nothing may stand on it, nobody walks on it.</summary>
    private void AddTrophy(int x, int y, double z)
    {
        var trophy = new RoomFloorItem
        {
            ObjectId = TROPHY,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 280,
                SpriteId = 280,
                Name = "legotrophy",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
        };

        trophy.SetExtraData(null);
        trophy.SetPosition(x, y);
        trophy.SetPositionZ(Altitude.FromValue(z));
        trophy.SetRotation(Rotation.North);
        trophy.SetLogic(
            new FurnitureFloorLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, trophy)
            )
        );
        _room.Harness.State.ItemsById.Add(TROPHY, trophy);
        _room.Map.AddItem(trophy);
    }

    private async Task BuildMoverAsync(string mover, int[] picks)
    {
        if (mover == "move_rotate")
        {
            await BuildMoveRotateAsync(picks);

            return;
        }

        await AddClickTriggerAsync();

        if (mover == "rel_mov")
            _room.AddBox<WiredActionRelativeMove>(ACTION, 0, 0, "wf_act_rel_mov");
        else
            _room.AddBox<WiredActionMoveToDirection>(ACTION, 0, 0, "wf_act_move_to_dir");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                ACTION,
                // Relative Move [0,-1]; Move To Direction north, wait when blocked, users block.
                intParams: mover == "rel_mov" ? [0, -1] : [0, 0, 1],
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StackChangedAsync();
    }

    private async Task BuildMoveRotateAsync(int[] picks)
    {
        await AddClickTriggerAsync();
        _room.AddBox<WiredActionMoveRotateFurni>(ACTION, 0, 0, "wf_act_move_rotate");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                ACTION,
                intParams: [MOVE_NORTH, 0],
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StackChangedAsync();
    }

    private async Task BuildRelativeMoveAsync(int? keepAltitude)
    {
        await AddClickTriggerAsync();
        _room.AddBox<WiredActionRelativeMove>(ACTION, 0, 0, "wf_act_rel_mov");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                ACTION,
                intParams: [1, 0],
                stuffIds: [MOVER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        if (keepAltitude is { } keep)
        {
            _room.AddBox<WiredAddonMovementPhysics>(PHYSICS, 0, 0, "wf_xtra_mov_physics");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    PHYSICS,
                    intParams: [keep, 0, 0, 0],
                    furniSources:
                    [
                        [WiredFurniSourceType.AllRoomItems],
                        [WiredFurniSourceType.AllRoomItems],
                    ],
                    playerSources:
                    [
                        [WiredPlayerSourceType.AllRoomUsers],
                    ]
                )
            ).Should().BeTrue();
        }

        await StackChangedAsync();
    }

    private async Task AddClickTriggerAsync()
    {
        _room.AddFloorItem(CLICK_ME, 11, 11);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
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
    }

    private async Task StackChangedAsync()
    {
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

    /// <summary>The furni moves one click sent the room, "id from z -> to z".</summary>
    private async Task<List<string>> ClickAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return
        [
            .. _room
                .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
                .Skip(before)
                .SelectMany(p => p.FloorItems)
                .Select(m =>
                    $"{(int)m.ObjectId} {m.SourceX},{m.SourceY} z{m.SourceZ.Value} -> {m.TargetX},{m.TargetY} z{m.TargetZ.Value}"
                ),
        ];
    }

    private List<(int X, int Y)> HeightsSent() =>
        [
            .. _room
                .Harness.Room.TakeHeightMapUpdates()
                .OfType<HeightMapUpdateMessageComposer>()
                .SelectMany(u => u.TileHeights)
                .Select(h => (h.X, h.Y)),
        ];

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
