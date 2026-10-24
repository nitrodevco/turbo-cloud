using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The furni movers that keep a picked wall item move it (official client capture 2026-10-24,
/// WIN63-202610201032, W1 lab in Hutch.'s model_h room, the room built here from its recorded
/// FloorHeightMap): Relative Move and Move and Rotate step its wall coordinates, along the wall
/// with its offsets kept, off the wall onto a floor tile with its height offset 147 lower plus 32
/// per floor level, and on over the floor keeping its altitude (action.33.X11 / action.4.X11,
/// 19-22-52 and 19-24-34, 2/2-3/3 each; action.33.B11 / action.57.B11, 17-49-51, 2/2); a rotation
/// turns it onto the other wall side in an ItemUpdate (action.4.B11, 2/2); Match Snapshot puts it
/// back where it was saved with its state, every firing (action.3.B11, 2/2). Ours never moved a
/// wall item.
/// </summary>
public sealed class WiredWallItemMoveTests
{
    private const int TRIGGER = 1;
    private const int ACTION = 2;
    private const int CLICK_ME = 20;
    private const int WALL_ITEM = 70;
    private const int HUTCH = 5;

    // model_h as the official FloorHeightMap sent it (2026-10-24T19-22-52, room 80462556).
    private const string MODEL_H =
        "xxxxxxxxxxxx\rxxxxxxxxxxxx\rxxxxx111111x\rxxxxx111111x\rxxxx1111111x\rxxxxx111111x\r"
        + "xxxxx111111x\rxxxxx000000x\rxxxxx000000x\rxxx00000000x\rxxx00000000x\rxxx00000000x\r"
        + "xxx00000000x\rxxxxxxxxxxxx\rxxxxxxxxxxxx\rxxxxxxxxxxxx";

    private readonly WiredRoom _room = new(model: ModelH());
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // Hutch. clicks the furni from 7,11, out of every wall item's way.
    public WiredWallItemMoveTests() => _room.Enter(HUTCH, 7, 11);

    // action.33.X11: clock "l" 2,10 (55 -> -92), wardrobe "r" 4,8 (89 -> -58) and window "l"
    // 4,2 next to a level 1 floor (70 -> -45); the second step keeps the height.
    [Theory]
    [InlineData(2, 10, 4, 55, false, -92)]
    [InlineData(4, 8, 0, 89, true, -58)]
    [InlineData(4, 2, 3, 70, false, -45)]
    public async Task Relative_move_steps_a_wall_item_off_the_wall_and_on(
        int x,
        int y,
        int offsetX,
        int offsetY,
        bool right,
        int offFloorY
    )
    {
        PlaceWallItem(x, y, offsetX, offsetY, right);
        await BuildAsync<WiredActionRelativeMove>("wf_act_rel_mov", [1, 0]);

        await ClickAsync();
        await ClickAsync();

        WallMoves()
            .Should()
            .Equal(
                (x, y, offsetX, offsetY, x + 1, y, offsetX, offFloorY, right),
                (x + 1, y, offsetX, offFloorY, x + 2, y, offsetX, offFloorY, right)
            );
    }

    // action.4.X11: [4,0,0] = y-1 along the wall, offsets and side kept, no ItemUpdate.
    [Theory]
    [InlineData(2, 10, 4, 55, false)]
    [InlineData(4, 8, 0, 89, true)]
    public async Task Move_and_rotate_moves_a_wall_item_along_the_wall(
        int x,
        int y,
        int offsetX,
        int offsetY,
        bool right
    )
    {
        PlaceWallItem(x, y, offsetX, offsetY, right);
        await BuildAsync<WiredActionMoveRotateFurni>("wf_act_move_rotate", [4, 0]);

        await ClickAsync();
        await ClickAsync();

        WallMoves()
            .Should()
            .Equal(
                (x, y, offsetX, offsetY, x, y - 1, offsetX, offsetY, right),
                (x, y - 1, offsetX, offsetY, x, y - 2, offsetX, offsetY, right)
            );
        _room.Harness.Fakes.SentToRoom<ItemUpdateMessageComposer>().Should().BeEmpty();
    }

    // action.4.X11: [5,0,0] (+x) on the "r" wardrobe = Relative Move [1,0].
    [Fact]
    public async Task Move_and_rotate_east_steps_a_wall_item_off_the_wall()
    {
        PlaceWallItem(4, 8, 0, 89, right: true);
        await BuildAsync<WiredActionMoveRotateFurni>("wf_act_move_rotate", [5, 0]);

        await ClickAsync();
        await ClickAsync();

        WallMoves()
            .Should()
            .Equal((4, 8, 0, 89, 5, 8, 0, -58, true), (5, 8, 0, -58, 6, 8, 0, -58, true));
    }

    // action.4.B11: [4,1,1] on the "r" wardrobe 4,8 0,89 -> ":w=4,7 l=32,89 l" -> ":w=4,6 l=0,89 r",
    // ItemUpdate and no wall move.
    [Fact]
    public async Task Move_and_rotate_with_a_rotation_turns_a_wall_item_in_an_item_update()
    {
        PlaceWallItem(4, 8, 0, 89, right: true);
        await BuildAsync<WiredActionMoveRotateFurni>("wf_act_move_rotate", [4, 1]);

        await ClickAsync();
        await ClickAsync();

        _room
            .Harness.Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(x => x.WallItem.WallPosition)
            .Should()
            .Equal(":w=4,7 l=32,89 l", ":w=4,6 l=0,89 r");
        WallMoves().Should().BeEmpty();
    }

    // action.57.B11: [1,0,0] with Hutch. on 7,11 (level 0): 6,6 (level 1) -26 -> 7,11 -58, then
    // sent where it is.
    [Fact]
    public async Task Move_as_group_takes_a_wall_item_to_the_user_and_sends_it_there_after()
    {
        PlaceWallItem(6, 6, 0, -26, right: true);
        await BuildAsync<WiredActionMoveAsGroup>(
            "wf_act_move_furni_as_group",
            [1, 0, 0],
            users: true
        );

        await ClickAsync();
        await ClickAsync();

        WallMoves()
            .Should()
            .Equal((6, 6, 0, -26, 7, 11, 0, -58, true), (7, 11, 0, -58, 7, 11, 0, -58, true));
    }

    // action.3.B11: [1,1,1,0] saved with the wardrobe on 4,8 0,89; at 7,11 -58 it goes back, its
    // state sent with it, and both again on the next firing.
    [Fact]
    public async Task Match_snapshot_puts_a_wall_item_back_and_sends_it_every_firing()
    {
        var wall = PlaceWallItem(4, 8, 0, 89, right: true);
        await BuildAsync<WiredActionMatchToSnapshot>("wf_act_match_to_sshot", [1, 1, 1, 0]);

        wall.SetPosition(7, 11);
        wall.SetPositionZ(Altitude.FromValue(-58));

        await ClickAsync();
        await ClickAsync();

        WallMoves()
            .Should()
            .Equal((7, 11, 0, -58, 4, 8, 0, 89, true), (4, 8, 0, 89, 4, 8, 0, 89, true));
        _room
            .Harness.Fakes.SentToRoom<ItemsStateUpdateMessageComposer>()
            .SelectMany(x => x.ObjectStates)
            .Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Item1 == WALL_ITEM);
    }

    private List<(int, int, int, int, int, int, int, int, bool)> WallMoves() =>
        [
            .. _room
                .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
                .SelectMany(x => x.WallItems)
                .Select(Row),
        ];

    private static (int, int, int, int, int, int, int, int, bool) Row(
        WiredWallItemMovementSnapshot m
    ) =>
        (
            m.SourceX,
            m.SourceY,
            m.SourceOffsetX,
            m.SourceOffsetY,
            m.TargetX,
            m.TargetY,
            m.TargetOffsetX,
            m.TargetOffsetY,
            m.IsDirectionRight
        );

    private Turbo.Rooms.Object.Furniture.Wall.RoomWallItem PlaceWallItem(
        int x,
        int y,
        int offsetX,
        int offsetY,
        bool right
    )
    {
        var wall = _room.AddWallItem(WALL_ITEM);

        wall.SetPosition(x, y);
        wall.SetPositionZ(Altitude.FromValue(offsetY));
        wall.SetRotation(right ? Rotation.West : Rotation.South);
        wall.SetWallOffset(offsetX);

        return wall;
    }

    private async Task BuildAsync<T>(string logic, int[] intParams, bool users = false)
        where T : class, IRoomObjectLogic
    {
        _room.AddFloorItem(CLICK_ME, 9, 12);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 10, 12, "wf_trg_click_furni");
        _room.AddBox<T>(ACTION, 10, 12, logic);

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
                ACTION,
                intParams: intParams,
                stuffIds: [-WALL_ITEM],
                furniSources: users
                    ?
                    [
                        [WiredFurniSourceType.SelectedItems],
                        [WiredFurniSourceType.TriggeredItem],
                    ]
                    :
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                playerSources: users
                    ?
                    [
                        [WiredPlayerSourceType.TriggeredUser],
                    ]
                    : null,
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(10, 12)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room.FloorItem(CLICK_ME).Logic.OnClickAsync(_room.ContextFor(HUTCH), 0, Ct);
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

    private static RoomModelSnapshot ModelH()
    {
        var model = RoomModelProvider.CleanModelString(MODEL_H);
        var compiled = RoomModelProvider.CompileModelFromString(model);

        return new RoomModelSnapshot
        {
            Id = 1,
            Name = "model_h",
            Model = model,
            DoorX = 4,
            DoorY = 4,
            DoorRotation = Rotation.East,
            Width = compiled.Width,
            Height = compiled.Height,
            Size = compiled.Width * compiled.Height,
            BaseHeights = compiled.Heights,
            BaseFlags = compiled.Flags,
        };
    }
}
