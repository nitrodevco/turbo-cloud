using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Move and Rotate Furni turning furni clockwise in place, as Habbo turns them (official client
/// capture 2026-10-25, W1 lab wlab10, own room re-entered first): a 2x1 sofa whose turn would
/// leave the map or run into a furni, 1x1 furni beside it, and "Can Perform Movements" over them.
/// A click runs the stack; a switch it toggles shows whether its effects ran.
/// </summary>
public sealed class WiredRotationLabTests
{
    private const int TRIGGER = 1;
    private const int CONDITION = 2;
    private const int TOGGLE = 3;
    private const int ROTATE = 4;

    private const int CLICK_ME = 20;
    private const int SWITCH = 21;
    private const int SOFA = 22;
    private const int LAMP = 23;
    private const int CHAIR = 24;
    private const int BLOCK = 25;

    // The editor's "no move" and "rotate clockwise".
    private const int NO_MOVE = 0;
    private const int CLOCKWISE = 1;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredRotationLabTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(SWITCH, 6, 7);
    }

    /// <summary>
    /// action.4.B8 / condition.39.B8: the sofa at the east edge facing east (10,3-4) turned
    /// clockwise would leave the map: alone in the box with a lamp and a chair, the sofa stayed
    /// and the other two turned (2/2); with Can Perform Movements in the stack nothing ran (2/2),
    /// all or nothing over the picked furni (05-21-28, 05-21-57).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_turn_off_the_map_blocks_that_furni_or_with_the_condition_the_stack(
        bool validMoves
    )
    {
        var sofa = Add(SOFA, 7, 3, width: 2, Rotation.East);
        var lamp = Add(LAMP, 2, 2);
        var chair = Add(CHAIR, 3, 2);
        await BuildAsync(validMoves, SOFA, LAMP, CHAIR);

        await ClickAsync();

        sofa.Rotation.Should().Be(Rotation.East);
        (lamp.Rotation, chair.Rotation)
            .Should()
            .Be(validMoves ? (Rotation.North, Rotation.North) : (Rotation.East, Rotation.East));
        SwitchState().Should().Be(validMoves ? 0 : 1);
    }

    /// <summary>
    /// action.4.B1b / condition.39.B1b: the sofa at 9,3 facing north turned clockwise covers 9,3
    /// and 9,4. A lamp (nothing stacks on it) on 9,4 blocked the turn and failed Can Perform
    /// Movements (2/2); a stackable block there instead let it turn and rise onto the block (dir
    /// 2, z 2 on a block at z 1, 1/1) (05-33-12).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_turn_onto_a_furni_rises_onto_a_stackable_one_and_is_blocked_by_another(
        bool stackable
    )
    {
        var sofa = Add(SOFA, 3, 3, width: 2, Rotation.North);
        Add(BLOCK, 3, 4, canStack: stackable);
        await BuildAsync(validMoves: true, SOFA);

        await ClickAsync();

        if (stackable)
            (sofa.Rotation, sofa.Z).Should().Be((Rotation.East, Altitude.FromInt(100)));
        else
            (sofa.Rotation, sofa.Z).Should().Be((Rotation.North, Altitude.Zero));

        SwitchState().Should().Be(stackable ? 1 : 0);
    }

    /// <summary>
    /// action.4.B9b: the sofa that rose onto the block kept its z 2 on every later turn (dir 4, 6,
    /// 0, 2), floating off the block, 4/4 (05-33-38).
    /// </summary>
    [Fact]
    public async Task A_furni_turned_up_onto_a_block_keeps_its_height_on_later_turns()
    {
        var sofa = Add(SOFA, 3, 3, width: 2, Rotation.North);
        Add(BLOCK, 3, 4);
        await BuildAsync(validMoves: false, SOFA);

        await ClickAsync();
        var risen = sofa.Z;
        risen.Should().Be(Altitude.FromInt(100));

        var seen = new List<(Rotation, Altitude)>();

        for (var i = 0; i < 4; i++)
        {
            await ClickAsync();
            seen.Add((sofa.Rotation, sofa.Z));
        }

        seen.Should()
            .Equal(
                (Rotation.South, risen),
                (Rotation.West, risen),
                (Rotation.North, risen),
                (Rotation.East, risen)
            );
    }

    private IRoomFloorItem Add(
        int id,
        int x,
        int y,
        int width = 1,
        Rotation rotation = Rotation.North,
        bool canStack = true
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + id,
                SpriteId = 1000 + id,
                Name = "furni" + id,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 2,
                Width = width,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = canStack,
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

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(rotation);
        item.SetLogic(
            new FurnitureFloorLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );
        _room.Harness.State.ItemsById.Add(id, item);
        _room.Map.AddItem(item);

        return item;
    }

    private int SwitchState() => _room.FloorItem(SWITCH).Logic.GetState();

    private async Task BuildAsync(bool validMoves, params int[] picks)
    {
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(TOGGLE, 0, 0, "wf_act_toggle_state");
        _room.AddBox<WiredActionMoveRotateFurni>(ROTATE, 0, 0, "wf_act_move_rotate");

        if (validMoves)
        {
            _room.AddBox<WiredConditionValidMoves>(CONDITION, 0, 0, "wf_cnd_valid_moves");
            (await _room.SaveAsync<UpdateConditionMessage>(CONDITION, definitionSpecifics: [0]))
                .Should()
                .BeTrue();
        }

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
        (
            await _room.SaveAsync<UpdateActionMessage>(
                ROTATE,
                intParams: [NO_MOVE, CLOCKWISE],
                stuffIds: picks,
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
