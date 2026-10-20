using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who holds a furni and in which room is written when it changes, through the furniture custody,
/// not on the room's next flush (docs/movements.md "The room"): a furni just picked up is the
/// inventory's at once, a room it has left can no longer write it out of the room it went to,
/// and each placement and pickup is one movement under its cause.
/// </summary>
public sealed class RoomFurnitureCustodyTests : IDisposable
{
    private const string GRAIN = "Turbo.Rooms.Grains.RoomPersistenceGrain";

    // The room harness is room 1, and its furni belong to player 1.
    private const int OWNER = 1;
    private const int OTHER = 9;
    private const int ROOM_A = 1;
    private const int ROOM_B = 6;
    private const int CHAIR = 2;
    private const int ITEM = 500;

    private readonly FurnitureWorld _world = new();
    private readonly WiredRoom _room = new(40, 40);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomFurnitureCustodyTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddPlayer(OTHER, "other");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.AddRoom(ROOM_A, OWNER);
        _world.AddRoom(ROOM_B, OWNER);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_furni_picked_up_can_be_traded_before_the_room_flushes()
    {
        var inventory = await InRoomAAsync(ITEM);

        await PickUpAsync(ITEM);

        (await inventory.TransferFurnitureAsync([ITEM], OTHER, TestCustody.Cause(), Ct))
            .Should()
            .BeTrue("the furni is in the owner's inventory");
        _world.Row(ITEM).PlayerEntityId.Should().Be(OTHER);
    }

    [Fact]
    public async Task A_furni_picked_up_in_one_room_and_placed_in_another_stays_in_the_second()
    {
        await InRoomAAsync(ITEM);
        // A move the first room has queued but not written yet.
        await RoomA().EnqueueDirtyItemsAsync(ROOM_A, [Item(ITEM, x: 2, y: 2)], Ct);

        await PickUpAsync(ITEM);
        var roomB = NewPersistence(ROOM_B);
        var placed = Item(ITEM, x: 4, y: 5);
        await ((IRoomPersistenceGrain)roomB).WritePlacementAsync(ROOM_B, placed, Cause(), Ct);
        await ((IRoomPersistenceGrain)roomB).EnqueueDirtyItemsAsync(ROOM_B, [placed], Ct);

        // Two rooms, two timers: the second room happens to write first.
        await FlushAsync(roomB);
        await FlushAsync(RoomA());

        var row = _world.Row(ITEM);
        (row.RoomEntityId, row.X, row.Y).Should().Be((ROOM_B, 4, 5));
    }

    [Fact]
    public async Task A_pickup_is_one_movement_from_the_spot_in_the_room_to_the_inventory()
    {
        await InRoomAAsync(ITEM, x: 3, y: 4);

        await PickUpAsync(ITEM);

        var row = _world.Movements().Should().ContainSingle().Subject;
        (row.Cause, row.Actor, row.AssetId, row.DefinitionId)
            .Should()
            .Be((MovementCauses.ROOM_PICKUP, OWNER, (long)ITEM, CHAIR));
        (row.FromKind, row.FromOwner, row.FromPlace, row.X, row.Y)
            .Should()
            .Be((PlaceKind.Room, OWNER, (long)ROOM_A, 3, 4));
        (row.ToKind, row.ToOwner).Should().Be((PlaceKind.Inventory, OWNER));
        _world.Row(ITEM).RoomEntityId.Should().BeNull("written at once, not on the flush");
    }

    [Fact]
    public async Task A_placement_is_written_at_once_as_one_movement_into_the_room()
    {
        _world.AddFurni(ITEM, OWNER, CHAIR, roomId: null);
        var inventory = await _world.NewInventoryAsync(OWNER, Ct);
        Use(inventory);
        var item = new RoomFloorItem
        {
            ObjectId = ITEM,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = _room.AddFloorItem(1, 39, 39).Definition with { Id = CHAIR },
        };
        item.SetExtraData(null);
        _room.Harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = _ => item;

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .PlaceFloorItemAsync(
                    ActionContext.CreateForPlayer(OWNER, ROOM_A),
                    (FurnitureItemSnapshot)
                        RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot)),
                    3,
                    4,
                    Rotation.East,
                    Ct
                )
        )
            .Should()
            .BeTrue();

        var row = _world.Row(ITEM);
        (row.RoomEntityId, row.X, row.Y, row.Rotation)
            .Should()
            .Be((ROOM_A, 3, 4, Rotation.East), "written before the next flush");
        var moved = _world.Movements().Should().ContainSingle().Subject;
        (moved.Cause, moved.FromKind, moved.ToKind, moved.ToPlace, moved.X, moved.Y)
            .Should()
            .Be((MovementCauses.ROOM_PLACE, PlaceKind.Inventory, PlaceKind.Room, ROOM_A, 3, 4));
    }

    [Fact]
    public async Task Taking_back_a_thousand_furni_is_one_write_under_one_operation()
    {
        const int count = 1000;
        var inventory = await _world.NewInventoryAsync(OWNER, Ct);
        Use(inventory);
        for (var i = 0; i < count; i++)
        {
            _world.AddFurni(ITEM + i, OWNER, CHAIR, ROOM_A, x: i % 40, y: i / 40);
            _room.AddFloorItem(ITEM + i, i % 40, i / 40, definitionId: CHAIR);
        }

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .PickUpAllOwnedAsync(ActionContext.CreateForPlayer(OWNER, ROOM_A), false, Ct)
        )
            .Should()
            .Be(count);

        _world.Rows().Should().OnlyContain(x => x.RoomEntityId == null);
        var moved = _world.Movements();
        moved.Should().HaveCount(count);
        moved.Select(x => x.OperationId).Distinct().Should().ContainSingle("one :pickall");
        moved.Select(x => (int)x.Seq).Should().Equal(Enumerable.Range(0, count));
        moved.Should().OnlyContain(x => x.Cause == MovementCauses.ROOM_PICKALL);
        (await inventory.GetItemSnapshotAsync(ITEM + count - 1, Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_furni_used_up_leaves_the_room_for_nowhere_with_what_it_was()
    {
        await InRoomAAsync(ITEM, x: 2, y: 3);
        var cause = MovementCause.New(MovementCauses.USE_EFFECT_BOX, OWNER, ITEM);

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .DeleteItemByIdAsync(ActionContext.CreateForPlayer(OWNER, ROOM_A), ITEM, cause, Ct)
        )
            .Should()
            .BeTrue();

        _world.Rows().Should().BeEmpty();
        var moved = _world.Movements().Should().ContainSingle().Subject;
        (moved.OperationId, moved.FromKind, moved.FromPlace, moved.X, moved.Y, moved.ToKind)
            .Should()
            .Be((cause.OperationId, PlaceKind.Room, (long)ROOM_A, 2, 3, PlaceKind.None));
        moved.SnapshotExtraData.Should().NotBeNull("a destruction carries what it was");
    }

    // The caller grants what a used-up item was worth once the delete returns. A failed write
    // used to be queued and the delete returned true: the grant was kept, and a crash before the
    // next flush kept the item too.
    [Fact]
    public async Task A_use_whose_write_fails_is_refused_and_the_furni_keeps_its_row()
    {
        await InRoomAAsync(ITEM, x: 2, y: 3);
        var persistence = NewPersistence(ROOM_A, new FailingOnceDb(_world.Db));
        _room.Harness.Fakes.Instances[(typeof(IRoomPersistenceGrain), (long)ROOM_A)] = persistence;
        var cause = MovementCause.New(MovementCauses.USE_EFFECT_BOX, OWNER, ITEM);

        var use = () =>
            _room
                .Harness.Module<RoomActionModule>()
                .DeleteItemByIdAsync(ActionContext.CreateForPlayer(OWNER, ROOM_A), ITEM, cause, Ct);

        await use.Should().ThrowAsync<InvalidOperationException>("nothing may be granted for it");

        await FlushAsync(persistence);

        (_world.Row(ITEM).RoomEntityId, _world.Row(ITEM).X, _world.Row(ITEM).Y)
            .Should()
            .Be((ROOM_A, 2, 3), "a refused use is not written later either");
        _world.Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task A_pickup_whose_first_write_fails_is_written_by_the_next_flush()
    {
        _world.AddFurni(ITEM, OWNER, CHAIR, ROOM_A);
        var db = new FailingOnceDb(_world.Db);
        var grain = NewPersistence(ROOM_A, db);

        await ((IRoomPersistenceGrain)grain).WritePickupAsync(ROOM_A, [Item(ITEM)], Cause(), Ct);
        _world.Row(ITEM).RoomEntityId.Should().Be(ROOM_A, "the database was restarting");

        await FlushAsync(grain);

        _world.Row(ITEM).RoomEntityId.Should().BeNull("the owner picked it up");
        _world.Movements().Should().ContainSingle("written once, with its movement");
    }

    [Fact]
    public async Task Recording_off_writes_no_movements_and_the_rows_change_as_before()
    {
        _world.Config.Set(new() { Enabled = false });
        await InRoomAAsync(ITEM);

        await PickUpAsync(ITEM);

        _world.Movements().Should().BeEmpty();
        (_world.Row(ITEM).RoomEntityId, _world.Row(ITEM).PlayerEntityId).Should().Be((null, OWNER));
    }

    /// <summary>A furni of the owner standing in room A, in the database and in the room.</summary>
    private async Task<IInventoryGrain> InRoomAAsync(int id, int x = 1, int y = 1)
    {
        _world.AddFurni(id, OWNER, CHAIR, ROOM_A, x, y);
        _room.AddFloorItem(id, x, y, definitionId: CHAIR);

        var inventory = await _world.NewInventoryAsync(OWNER, Ct);
        Use(inventory);

        return inventory;
    }

    private async Task PickUpAsync(int id) =>
        (
            await _room.Harness.Room.RemoveItemByIdAsync(
                ActionContext.CreateForPlayer(OWNER, ROOM_A),
                id,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();

    /// <summary>The room reaches this inventory and a real persistence grain over the database.</summary>
    private void Use(IInventoryGrain inventory)
    {
        _room.Harness.Fakes.Instances[(typeof(IInventoryGrain), (long)OWNER)] = inventory;
        _room.Harness.Fakes.Instances[(typeof(IRoomPersistenceGrain), (long)ROOM_A)] =
            NewPersistence(ROOM_A);
    }

    private IRoomPersistenceGrain RoomA() =>
        (IRoomPersistenceGrain)
            _room.Harness.Fakes.Instances[(typeof(IRoomPersistenceGrain), (long)ROOM_A)];

    private object NewPersistence(int roomId, IDbContextFactory<TurboDbContext>? db = null)
    {
        db ??= _world.Db;
        var grain = GrainHarness.Create(typeof(RoomGrain).Assembly, GRAIN, _world.Fakes, db);
        RoomHarness.SetMember(
            RoomHarness.GetField(grain, "_state")!,
            "RoomId",
            RoomId.Parse(roomId)
        );
        RoomHarness.SetField(
            grain,
            "_custody",
            TestCustody.Over(db, _world.NewRecorderAsync(Ct).GetAwaiter().GetResult())
        );

        return grain;
    }

    private static MovementCause Cause() => MovementCause.New("test", OWNER);

    private static Task FlushAsync(object grain) =>
        (Task)
            grain
                .GetType()
                .GetMethod(
                    "FlushDirtyItemsAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    [typeof(CancellationToken)]
                )!
                .Invoke(grain, [CancellationToken.None])!;

    private static RoomFloorItemSnapshot Item(int id, int x = 1, int y = 1) =>
        new()
        {
            ObjectId = id,
            OwnerId = OWNER,
            OwnerName = "owner",
            DefinitionId = CHAIR,
            SpriteId = CHAIR,
            X = x,
            Y = y,
            Z = 0,
            Rotation = Rotation.North,
            StackHeight = 1,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = string.Empty,
            UsagePolicy = FurnitureUsageType.Everybody,
        };

    /// <summary>A database whose first context throws, as one that is restarting would.</summary>
    private sealed class FailingOnceDb(IDbContextFactory<TurboDbContext> inner)
        : IDbContextFactory<TurboDbContext>
    {
        private bool _failed;

        public TurboDbContext CreateDbContext() => Next();

        public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(Next());

        private TurboDbContext Next()
        {
            if (_failed)
                return inner.CreateDbContext();

            _failed = true;

            throw new InvalidOperationException("db restarting");
        }
    }
}
