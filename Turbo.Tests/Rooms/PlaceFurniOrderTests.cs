using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A furni placed from the inventory: Habbo answers ObjectAdd, HeightMapUpdate, FurniListRemove
/// (official client captures, 3 of 3: hcbox-full, hc-rights-test, 2026-10-13T21-39-07). Turbo
/// sent the inventory's removal first, straight to the player, the furni through the room stream
/// after it and the heights on the next tick.
/// </summary>
public sealed class PlaceFurniOrderTests
{
    private const int OWNER = 1;
    private const int OTHER = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_owner_is_told_the_furni_then_the_heights_then_their_inventory_loses_it()
    {
        await PlaceAsync();

        var toOwner = _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Key is long key
                && key == OWNER
                && (
                    x.Interface == typeof(IPlayerPresenceGrain) && x.Method == "SendComposerAsync"
                    || x.Interface == typeof(IInventoryGrain) && x.Method == "RemoveFurnitureAsync"
                )
            )
            .Select(x =>
                x.Method == "RemoveFurnitureAsync" ? "FurniListRemove" : x.Args[0]!.GetType().Name
            )
            .ToList();

        toOwner
            .Should()
            .Equal(
                nameof(ObjectAddMessageComposer),
                nameof(HeightMapUpdateMessageComposer),
                "FurniListRemove"
            );
    }

    [Fact]
    public async Task Everyone_else_gets_the_furni_and_the_heights_together_through_the_room()
    {
        await PlaceAsync();

        var published = _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => (RoomOutboundSnapshot)call.Args[0]!)
            .Single(x => x.Composers.Any(c => c is ObjectAddMessageComposer));

        published
            .Composers.Select(x => x.GetType())
            .Should()
            .Equal(typeof(ObjectAddMessageComposer), typeof(HeightMapUpdateMessageComposer));
        published.ExcludedPlayerIds.Should().Equal((PlayerId)OWNER);
    }

    /// <summary>
    /// A wall item the other way round: FurniListRemove, then ItemAdd (official client captures
    /// 2026-10-16 09:03-09:04, 2 of 2).
    /// </summary>
    [Fact]
    public async Task A_wall_item_leaves_the_inventory_before_the_room_is_shown_it()
    {
        var item = _room.AddWallItem(40);
        (
            (System.Collections.IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!
        ).Remove((RoomObjectId)40);
        _room.Harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = _ => item;
        _room.Harness.Fakes.Log.Calls.Clear();

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .PlaceWallItemAsync(
                    ActionContext.CreateForPlayer(OWNER, 1),
                    (FurnitureItemSnapshot)
                        RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot)),
                    0,
                    2,
                    Altitude.Zero,
                    4,
                    Rotation.North,
                    Ct
                )
        )
            .Should()
            .BeTrue();

        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Interface == typeof(IInventoryGrain) && x.Method == "RemoveFurnitureAsync"
                || x.Method == "OnNextAsync"
                    && ((RoomOutboundSnapshot)x.Args[0]!).Composers.Any(c =>
                        c is ItemAddMessageComposer
                    )
            )
            .Select(x => x.Method)
            .Should()
            .Equal("RemoveFurnitureAsync", "OnNextAsync");
    }

    private async Task PlaceAsync()
    {
        var item = new RoomFloorItem
        {
            ObjectId = 20,
            OwnerId = OWNER,
            OwnerName = "test",
            Definition = _room.AddFloorItem(1, 7, 7).Definition with { Id = 30, SpriteId = 30 },
        };
        item.SetExtraData(null);
        _room.Harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = _ => item;

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .PlaceFloorItemAsync(
                    ActionContext.CreateForPlayer(OWNER, 1),
                    (FurnitureItemSnapshot)
                        RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot)),
                    3,
                    3,
                    Rotation.North,
                    Ct
                )
        )
            .Should()
            .BeTrue();
    }
}
