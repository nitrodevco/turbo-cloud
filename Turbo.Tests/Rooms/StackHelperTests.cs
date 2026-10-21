using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The stacking helper (<c>tile_stackmagic*</c>, logic <c>custom_stack_height</c>). Its window
/// (WIN63-202610151745 <c>CustomStackHeightWidget</c>) sends a height, -100 for "place on top"
/// and, from Move up / Move down, SetAdjacentCustomStackingHeight(furniId, down)
/// (<c>CustomStackHeightWidget.as:187-195</c>, <c>:276</c>). It reads the helper's height back
/// from where the furni stands (<c>FurnitureCustomStackHeightWidgetHandler.update</c>), so the
/// move's ObjectUpdate is the whole answer. "Adjust the height of stacking helper as you want,
/// then move the other furni on top of it" (<c>widget.custom.stack.height.text</c>): its tiles
/// are as high as it is, whatever else stands there.
/// </summary>
public sealed class StackHelperTests
{
    private const int HELPER = 30;
    private const int LOW = 31;
    private const int HIGH = 32;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private readonly ActionContext _owner = ActionContext.CreateForSystem(1);

    private IRoomFloorItem AddHelper(int x, int y)
    {
        var helper = _room.AddFloorItem(
            HELPER,
            x,
            y,
            "custom_stack_height",
            canWalk: false,
            createLogic: (factory, ctx) => new FurnitureStackTileLogic(factory, ctx)
        );

        // Habbo's helpers have no height of their own and nothing may stack on them by data.
        helper.UseDefinition(
            helper.Definition with
            {
                StackHeight = Altitude.Zero,
                CanStack = false,
            }
        );
        _room.Map.ComputeTile(x, y);

        return helper;
    }

    /// <summary>A 1-high block standing at <paramref name="z"/> on a tile.</summary>
    private void AddBlock(int id, int x, int y, double z)
    {
        var block = _room.AddFloorItem(id, x, y, canWalk: false);

        block.SetPositionZ(Altitude.FromValue(z));
        _room.Map.ComputeTile(x, y);
    }

    private Task<bool> MoveAsync(bool down) =>
        _room
            .FloorItem(HELPER)
            .Logic.OnInteractAsync(
                _owner,
                new SetAdjacentStackHeightInteraction { Down = down },
                Ct
            );

    private List<object> SentToRoom() =>
        [
            .. _room
                .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
                .OfType<RoomOutboundSnapshot>()
                .SelectMany(x => x.Composers),
        ];

    private async Task<List<double>> ClicksAsync(IRoomFloorItem helper, params bool[] downs)
    {
        var seen = new List<double>();

        foreach (var down in downs)
        {
            (await MoveAsync(down)).Should().BeTrue("every click is answered");
            seen.Add(helper.Z.Value);
        }

        return seen;
    }

    [Fact]
    public async Task Move_up_stacks_one_furni_higher_and_move_down_goes_to_the_next_top_then_the_floor()
    {
        // lab49 B: a 1-high table at 0 on the helper's tile, up x3 then down x3 (2 runs identical).
        var helper = AddHelper(2, 2);
        AddBlock(LOW, 2, 2, 0);

        (await ClicksAsync(helper, false, false, false, true, true, true))
            .Should()
            .Equal(1, 2, 3, 1, 0, 0);
    }

    [Fact]
    public async Task With_nothing_of_any_height_on_its_tile_the_helper_stays_and_is_sent_anyway()
    {
        // lab49 A (alone, 4/4) and B (a rug, 4/4); C: furni on the neighbouring tile play no part.
        var helper = AddHelper(2, 2);
        AddBlock(HIGH, 3, 2, 0);

        (await ClicksAsync(helper, false, false, true, true)).Should().Equal(0, 0, 0, 0);
        SentToRoom()
            .OfType<ObjectUpdateMessageComposer>()
            .Should()
            .HaveCount(4, "each click is answered with the helper's ObjectUpdate");

        var rug = _room.AddFloorItem(LOW, 2, 2, canWalk: true);
        rug.UseDefinition(rug.Definition with { StackHeight = Altitude.Zero });

        (await ClicksAsync(helper, false)).Should().Equal(0);
    }

    [Fact]
    public async Task The_helpers_tile_is_as_high_as_the_helper_whatever_else_stands_there()
    {
        var helper = AddHelper(2, 2);
        AddBlock(LOW, 2, 2, 0);
        AddBlock(HIGH, 2, 2, 1.5);

        _room
            .Map.GetTileHeight(_room.Map.ToIdx(2, 2))
            .Value.Should()
            .Be(0, "the helper still stands on the floor");

        await MoveAsync(down: false);

        helper.Z.Value.Should().Be(1.0);
        _room
            .Map.GetTileHeight(_room.Map.ToIdx(2, 2))
            .Value.Should()
            .Be(1.0, "not the 2.5 of the high block");
        _room
            .Harness.Module<Turbo.Rooms.Grains.Modules.RoomFurniModule>()
            .CanPlaceFloorItem(
                _room.AddFloorItem(40, 5, 5),
                2,
                2,
                Turbo.Primitives.Rooms.Enums.Rotation.North
            )
            .Should()
            .BeTrue("other furni are moved on top of it");
    }

    [Fact]
    public async Task A_move_is_told_to_the_room_as_the_helpers_object_update_only()
    {
        AddHelper(2, 2);
        AddBlock(LOW, 2, 2, 0);

        await MoveAsync(down: false);

        SentToRoom()
            .Select(x => x.GetType().Name)
            .Should()
            .Contain(nameof(ObjectUpdateMessageComposer))
            .And.OnlyContain(x =>
                x == nameof(ObjectUpdateMessageComposer) || x == "HeightMapUpdateMessageComposer"
            );
    }
}
