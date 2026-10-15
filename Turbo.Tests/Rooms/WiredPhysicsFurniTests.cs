using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Movement Physics on a furni move (official client capture, 2026-10-14, a block moved east by
/// "Move furni to direction"): "Move through furniture" lets the moved furni land on top of the
/// furni of the add-on's first source even when nothing may be stacked on them; "Is blocked by
/// furniture" stops it at the furni of the second source even when they are stackable; the
/// default source (900) is every furni; furni the source leaves out behave as without the add-on,
/// and a blocked move sends nothing. The add-on stored the flags but no move read them.
/// </summary>
public sealed class WiredPhysicsFurniTests
{
    private const int BLOCK = 30;
    private const int CHAIR = 31;
    private const int OTHER_CHAIR = 33;
    private const int STACKABLE = 32;
    private const int PHYSICS = 8;

    private readonly WiredRoom _room = new(10, 10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredPhysicsFurniTests()
    {
        _room.AddFloorItem(BLOCK, 1, 1);
        AddChair(CHAIR, 2, 1);
        AddChair(OTHER_CHAIR, 3, 1);
        _room.AddFloorItem(STACKABLE, 2, 2);
    }

    [Fact]
    public async Task Without_physics_a_chair_blocks_the_move()
    {
        var ctx = await ExecutionAsync(null);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 1)).Should().BeFalse();
        ctx.FloorItemMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Move_through_the_picked_chair_lands_on_top_of_it()
    {
        var ctx = await ExecutionAsync([0, 1, 0, 0], picked: CHAIR, slot: 0);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 1)).Should().BeTrue();

        (Block.X, Block.Y).Should().Be((2, 1));
        // The chair stands on the floor and is one tile high: the block sits on its top.
        Block.Z.Should().Be(Altitude.FromInt(100));
    }

    [Fact]
    public async Task Move_through_the_picked_chair_still_stops_at_a_chair_not_picked()
    {
        var ctx = await ExecutionAsync([0, 1, 0, 0], picked: CHAIR, slot: 0);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 1)).Should().BeTrue();

        (await ctx.TryMoveFloorItemAsync(Block, 3, 1)).Should().BeFalse();
        (Block.X, Block.Y).Should().Be((2, 1));
        ctx.FloorItemMoves.Should().HaveCount(1);
    }

    [Fact]
    public async Task Move_through_every_furni_by_default()
    {
        var ctx = await ExecutionAsync([0, 1, 0, 0]);

        (await ctx.TryMoveFloorItemAsync(Block, 3, 1)).Should().BeTrue();
    }

    [Fact]
    public async Task Without_physics_a_stackable_furni_is_stacked_on()
    {
        var ctx = await ExecutionAsync(null);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 2)).Should().BeTrue();
    }

    [Fact]
    public async Task Blocked_by_the_picked_stackable_furni_sends_nothing()
    {
        var ctx = await ExecutionAsync([0, 0, 0, 1], picked: STACKABLE, slot: 1);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 2)).Should().BeFalse();
        (Block.X, Block.Y).Should().Be((1, 1));
        ctx.FloorItemMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Blocked_by_another_furni_still_stacks_on_one_not_picked()
    {
        var ctx = await ExecutionAsync([0, 0, 0, 1], picked: CHAIR, slot: 1);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 2)).Should().BeTrue();
    }

    [Fact]
    public async Task Blocked_by_every_furni_by_default()
    {
        var ctx = await ExecutionAsync([0, 0, 0, 1]);

        (await ctx.TryMoveFloorItemAsync(Block, 2, 2)).Should().BeFalse();
    }

    private IRoomFloorItem Block => _room.FloorItem(BLOCK);

    private void AddChair(int id, int x, int y)
    {
        var chair = _room.AddFloorItem(id, x, y);

        chair
            .GetType()
            .GetProperty(nameof(chair.Definition))!
            .SetValue(chair, chair.Definition with { CanStack = false });
        _room.Map.ComputeTile(x, y);
    }

    /// <summary>
    /// The execution context of a stack whose Movement Physics add-on was saved with
    /// <paramref name="intParams"/> (null: no add-on), the picked furni going to one slot.
    /// </summary>
    private async Task<WiredExecutionContext> ExecutionAsync(
        int[]? intParams,
        int? picked = null,
        int slot = 0
    )
    {
        var processing = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        if (intParams is not null)
        {
            var addon = _room.AddBox<WiredAddonMovementPhysics>(
                PHYSICS,
                9,
                9,
                "wf_xtra_mov_physics"
            );
            WiredFurniSourceType[] Source(int s) =>
                picked is not null && s == slot
                    ? [WiredFurniSourceType.SelectedItems]
                    : [WiredFurniSourceType.AllRoomItems];

            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    PHYSICS,
                    intParams: intParams,
                    stuffIds: picked is { } id ? [id] : [],
                    furniSources: [Source(0), Source(1)],
                    playerSources:
                    [
                        [WiredPlayerSourceType.AllRoomUsers],
                    ]
                )
            ).Should().BeTrue();

            await addon.MutatePolicyAsync(processing, Ct);
        }

        return new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = processing.Policy,
            CancellationToken = Ct,
        };
    }
}
