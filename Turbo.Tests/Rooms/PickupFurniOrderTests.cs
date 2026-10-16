using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A furni picked up into the inventory: Habbo answers ObjectRemove, HeightMapUpdate,
/// FurniListAddOrUpdate for a floor item (official client captures 2026-10-13 21:38 and
/// 2026-10-16 09:03-09:04, 5 of 5) and ItemRemove, FurniListAddOrUpdate for a wall item (2 of 2).
/// Turbo sent the removal through the room stream, the heights on the next tick and the
/// inventory's update straight to the player, which could reach them first.
/// </summary>
public sealed class PickupFurniOrderTests
{
    private const int OWNER = 1;
    private const int CHAIR = 20;
    private const int WALL_ITEM = 30;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_owner_is_told_the_removal_then_the_heights_then_their_inventory_gets_it()
    {
        await PickUpAsync(ActionContext.CreateForPlayer(OWNER, 1));

        ToOwner()
            .Should()
            .Equal(
                nameof(ObjectRemoveMessageComposer),
                nameof(HeightMapUpdateMessageComposer),
                "FurniListAddOrUpdate"
            );
    }

    [Fact]
    public async Task Everyone_else_gets_the_removal_and_the_heights_together_through_the_room()
    {
        await PickUpAsync(ActionContext.CreateForPlayer(OWNER, 1));

        var published = Published()
            .Single(x => x.Composers.Any(c => c is ObjectRemoveMessageComposer));

        published
            .Composers.Select(x => x.GetType())
            .Should()
            .Equal(typeof(ObjectRemoveMessageComposer), typeof(HeightMapUpdateMessageComposer));
        published.ExcludedPlayerIds.Should().Equal((PlayerId)OWNER);
    }

    [Fact]
    public async Task A_wall_item_s_owner_is_told_the_removal_then_their_inventory_gets_it()
    {
        _room.AddWallItem(WALL_ITEM);
        _room.Harness.Fakes.Log.Calls.Clear();

        (
            await _room.Harness.Room.RemoveItemByIdAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                WALL_ITEM,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();

        ToOwner().Should().Equal(nameof(ItemRemoveMessageComposer), "FurniListAddOrUpdate");
    }

    [Fact]
    public async Task An_owner_who_is_not_here_is_not_sent_the_room_s_removal()
    {
        // The room sends a furni back to its owner (a group's furni when the group goes).
        await PickUpAsync(ActionContext.CreateForSystem(1));

        ToOwner().Should().Equal("FurniListAddOrUpdate");
        Published()
            .Single(x => x.Composers.Any(c => c is ObjectRemoveMessageComposer))
            .ExcludedPlayerIds.Should()
            .BeNullOrEmpty();
    }

    private async Task PickUpAsync(ActionContext picker)
    {
        _room.AddFloorItem(CHAIR, 3, 3);

        // The tick has already sent the heights the furni made when it was placed.
        ((HashSet<int>)RoomHarness.GetMember(_room.Harness.State, "DirtyHeightTileIds")!).Clear();
        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "DirtyHeightSentValues")!).Clear();
        _room.Harness.Fakes.Log.Calls.Clear();

        (await _room.Harness.Room.RemoveItemByIdAsync(picker, CHAIR, true, Ct)).Should().BeTrue();
    }

    private List<string> ToOwner() =>
        [
            .. _room
                .Harness.Fakes.Log.Calls.Where(x =>
                    x.Key is long key
                    && key == OWNER
                    && (
                        x.Interface == typeof(IPlayerPresenceGrain)
                            && x.Method == "SendComposerAsync"
                        || x.Interface == typeof(IInventoryGrain)
                            && x.Method
                                == nameof(IInventoryGrain.AddFurnitureFromRoomItemSnapshotAsync)
                    )
                )
                .Select(x =>
                    x.Interface == typeof(IInventoryGrain)
                        ? "FurniListAddOrUpdate"
                        : x.Args[0]!.GetType().Name
                ),
        ];

    private IEnumerable<RoomOutboundSnapshot> Published() =>
        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => (RoomOutboundSnapshot)call.Args[0]!);
}
