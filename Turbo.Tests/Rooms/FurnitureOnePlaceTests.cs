using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A furni is in one place: an inventory, a room, or held by another furni. Placing and picking
/// up said so only at the room's next save, a few seconds later, and the save wrote whatever the
/// room thought. So a furni could be placed after it was traded or deposited in a chest (listed
/// in two places, and duplicated by the next save), and a save written late put a furni back in
/// a room it had left. The database now decides as each move happens.
/// </summary>
public sealed class FurnitureOnePlaceTests : IDisposable
{
    private const int ROOM = 1;
    private const int OTHER_ROOM = 2;
    private const int ITEM = 40;
    private const int OWNER = 1;
    private const int BUYER = 2;
    private const int STAFF = 3;

    private static readonly ActionContext Owner = ActionContext.CreateForPlayer(OWNER, ROOM);

    private readonly SqliteDb _db = new();
    private readonly RoomHarness _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureOnePlaceTests()
    {
        _db.InsertOwnersAndRooms([OWNER, BUYER, STAFF], [ROOM, OTHER_ROOM]);
        _db.Insert(
            new FurnitureEntity
            {
                Id = ITEM,
                PlayerEntityId = OWNER,
                FurnitureDefinitionEntityId = FurnitureRows.DEFINITION,
            }
        );

        RoomHarness.SetField(_room.Room, "_dbCtxFactory", _db);
        // The room tells everyone in it what was placed and picked up.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(_room.Room, _room.Fakes.Create(stream.FieldType, "room-stream"));
        _room.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = _ =>
            _room.CreateFloorItem(ITEM, 0, 0, Altitude.Zero);

        OwnTheRoom();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_placed_furni_is_in_the_room_in_the_database_at_once()
    {
        (await PlaceAsync(3, 4)).Should().BeTrue();

        var row = await RowAsync();
        (row.RoomEntityId, row.X, row.Y).Should().Be((ROOM, 3, 4));
    }

    /// <summary>
    /// Traded away a moment before (the inventory the placement read from had not caught up):
    /// the furni stood in the room and was listed in the buyer's inventory.
    /// </summary>
    [Fact]
    public async Task A_furni_traded_away_meanwhile_is_not_placed_and_leaves_the_stale_inventory()
    {
        await UpdateRowAsync(row => row.PlayerEntityId = BUYER);

        (await PlaceAsync(3, 4)).Should().BeFalse();

        _room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
        (await RowAsync()).RoomEntityId.Should().BeNull();
        _room
            .Fakes.Log.Of(nameof(IInventoryGrain.ReleaseFurnitureAsync))
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)OWNER);
    }

    [Fact]
    public async Task A_furni_placed_in_another_room_meanwhile_is_not_placed_here()
    {
        await UpdateRowAsync(row => row.RoomEntityId = OTHER_ROOM);

        (await PlaceAsync(3, 4)).Should().BeFalse();

        _room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
        (await RowAsync()).RoomEntityId.Should().Be(OTHER_ROOM);
    }

    [Fact]
    public async Task A_placement_the_room_refuses_leaves_the_furni_in_the_inventory()
    {
        (await PlaceAsync(50, 50)).Should().BeFalse();

        (await RowAsync()).RoomEntityId.Should().BeNull();
    }

    /// <summary>
    /// Picked up, then placed in another room before this room's save: the save wrote it back
    /// into this room, in two rooms at once in memory and in this one in the database.
    /// </summary>
    [Fact]
    public async Task A_picked_up_furni_is_out_of_the_room_in_the_database_at_once()
    {
        await PlaceAsync(3, 4);

        (await _room.Room.RemoveItemByIdAsync(Owner, ITEM, Ct)).Should().BeTrue();

        var row = await RowAsync();
        (row.RoomEntityId, row.PlayerEntityId).Should().Be(((int?)null, OWNER));
        _room
            .Fakes.Log.Of(nameof(IInventoryGrain.AddFurnitureFromRoomItemSnapshotAsync))
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)OWNER);
    }

    [Fact]
    public async Task A_staff_member_who_keeps_what_they_pick_up_owns_it_and_its_history_says_so()
    {
        await PlaceAsync(3, 4);
        _room.Fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                Convert.ToInt64(call.Key) == STAFF
                    && (string)call.Args[0]! == PermissionNodes.Room.FURNI_STEAL
            );

        (await _room.Room.RemoveItemByIdAsync(ActionContext.CreateForPlayer(STAFF, ROOM), ITEM, Ct))
            .Should()
            .BeTrue();

        (await RowAsync()).PlayerEntityId.Should().Be(STAFF);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.FurnitureTransfers.SingleAsync(Ct))
            .Should()
            .Match<FurnitureTransferEntity>(x =>
                x.FurnitureId == ITEM
                && x.FromPlayerId == OWNER
                && x.ToPlayerId == STAFF
                && x.Reason == FurnitureTransferReason.StaffPickup
            );
    }

    [Fact]
    public async Task A_furni_the_database_does_not_have_in_the_room_is_not_listed_on_pickup()
    {
        await PlaceAsync(3, 4);
        await UpdateRowAsync(row => row.RoomEntityId = null);

        (await _room.Room.RemoveItemByIdAsync(Owner, ITEM, Ct)).Should().BeFalse();

        _room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
        _room
            .Fakes.Log.Of(nameof(IInventoryGrain.AddFurnitureFromRoomItemSnapshotAsync))
            .Should()
            .BeEmpty();
    }

    private Task<bool> PlaceAsync(int x, int y) =>
        _room.Room.PlaceFloorItemAsync(Owner, Snapshot(), x, y, Rotation.North, Ct);

    private async Task<FurnitureEntity> RowAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.Furnitures.AsNoTracking().SingleAsync(x => x.Id == ITEM, Ct);
    }

    private async Task UpdateRowAsync(Action<FurnitureEntity> change)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);

        change(row);

        await db.SaveChangesAsync(Ct);
    }

    private void OwnTheRoom()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;

        var info = (RoomSnapshot)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(RoomSnapshot)
            );

        RoomHarness.SetMember(_room.State, "IsRightsLoaded", true);
        RoomHarness.SetMember(info, "RoomId", (RoomId)ROOM);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)OWNER);
        RoomHarness.SetMember(_room.State, "RoomSnapshot", info);
    }

    private FurnitureItemSnapshot Snapshot()
    {
        var definition = _room.CreateFloorItem(ITEM, 0, 0, Altitude.Zero).Definition;

        return new FurnitureItemSnapshot
        {
            ItemId = ITEM,
            SpriteId = definition.SpriteId,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = definition,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
    }
}
