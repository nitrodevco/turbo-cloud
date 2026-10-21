using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The holder, room-made, borrowed and backstop transitions of the furniture custody
/// (docs/movements.md "Holders", "The room"): while recording, each furni is one movement under
/// the caller's cause, numbered from its first seq and written with the row; a refused change
/// writes none, and with recording off the rows change exactly as they did.
/// </summary>
public sealed class HolderCustodyMovementTests : IDisposable
{
    private const int OWNER = 8;
    private const int OTHER = 9;
    private const int ROOM = 5;
    private const int CHAIR = 1;
    private const int CHEST = 50;

    private readonly FurnitureWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HolderCustodyMovementTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddPlayer(OTHER, "other");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.AddRoom(ROOM, OWNER);
        _world.AddFurni(CHEST, OWNER, CHAIR, ROOM);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task FurniPutInAHolderAndTakenOut_MoveInventoryToHolderAndBack()
    {
        _world.AddFurni(10, OTHER, CHAIR, null);
        _world.AddFurni(11, OTHER, CHAIR, null);
        var custody = await CustodyAsync();
        var holder = new FurniHolder(CHEST, OWNER, 77);
        var putIn = MovementCause.New(MovementCauses.WIRED_CHEST_DEPOSIT, 0, 77);
        var takenOut = MovementCause.New(MovementCauses.WIRED_CHEST_WITHDRAW, 0, 78);

        (
            await InUnitAsync(db =>
                custody.PutInHolderAsync(db, OTHER, Items(10, 11), holder, putIn, Ct)
            )
        )
            .Should()
            .BeTrue();

        _world.Row(10).ChestItemEntityId.Should().Be(CHEST);
        _world.Row(10).PlayerEntityId.Should().Be(OWNER, "held for the chest's owner");
        _world.Row(10).ChestTransactionId.Should().Be(77);

        (
            await InUnitAsync(db =>
                custody.TakeOutOfHolderAsync(db, holder, Items(11), OTHER, takenOut, Ct)
            )
        )
            .Should()
            .BeTrue();

        _world.Row(11).ChestItemEntityId.Should().BeNull();
        _world.Row(11).ChestTransactionId.Should().BeNull();
        _world.Row(11).PlayerEntityId.Should().Be(OTHER);

        var rows = _world.Movements();
        rows.Should().HaveCount(3);
        rows.Take(2)
            .Select(x =>
                (
                    x.OperationId,
                    x.Seq,
                    x.AssetId,
                    x.FromKind,
                    x.FromOwner,
                    x.ToKind,
                    x.ToOwner,
                    x.ToPlace
                )
            )
            .Should()
            .Equal(
                (
                    putIn.OperationId,
                    (short)0,
                    10L,
                    PlaceKind.Inventory,
                    OTHER,
                    PlaceKind.Holder,
                    OWNER,
                    (long)CHEST
                ),
                (
                    putIn.OperationId,
                    (short)1,
                    11L,
                    PlaceKind.Inventory,
                    OTHER,
                    PlaceKind.Holder,
                    OWNER,
                    (long)CHEST
                )
            );
        var withdrawn = rows[2];
        (
            withdrawn.OperationId,
            withdrawn.FromKind,
            withdrawn.FromPlace,
            withdrawn.ToKind,
            withdrawn.ToOwner
        )
            .Should()
            .Be((takenOut.OperationId, PlaceKind.Holder, (long)CHEST, PlaceKind.Inventory, OTHER));
    }

    [Fact]
    public async Task AHolderMoveOfAFurniNotThere_ChangesNothing_AndRecordsNothing()
    {
        _world.AddFurni(10, OTHER, CHAIR, null);
        _world.AddFurni(11, OTHER, CHAIR, ROOM);
        var custody = await CustodyAsync();

        (
            await InUnitAsync(db =>
                custody.PutInHolderAsync(
                    db,
                    OTHER,
                    Items(10, 11),
                    new FurniHolder(CHEST, OWNER),
                    TestCustody.Cause(),
                    Ct
                )
            )
        )
            .Should()
            .BeFalse();

        _world.Row(10).ChestItemEntityId.Should().BeNull("all or none, and the caller rolls back");
        _world.Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task FurniUsedUpInAnotherUnitOfWork_AreDestroyedWithSnapshots()
    {
        _world.AddFurni(10, OTHER, CHAIR, null);
        _world.AddFurni(11, OTHER, CHAIR, null);
        var custody = await CustodyAsync();
        var cause = MovementCause.New(MovementCauses.WIRED_CHEST_DEPOSIT, 0, 5).StartingAt(2);

        (
            await InUnitAsync(db =>
                custody.DestroyInInventoryAsync(db, OTHER, Items(10, 11), cause, Ct)
            )
        )
            .Should()
            .BeTrue();

        _world.Rows().Select(x => x.Id).Should().Equal(CHEST);
        _world
            .Movements()
            .Select(x => (x.Seq, x.AssetId, x.FromKind, x.ToKind, x.SnapshotExtraData is not null))
            .Should()
            .Equal(
                ((short)2, 10L, PlaceKind.Inventory, PlaceKind.None, true),
                ((short)3, 11L, PlaceKind.Inventory, PlaceKind.None, true)
            );
    }

    [Fact]
    public async Task FurniMadeStraightIntoARoom_StandWhereTheySay_AndAreMadeFromNowhere()
    {
        var custody = await CustodyAsync();
        var cause = MovementCause.New(MovementCauses.ROOM_POST_IT, OTHER, CHEST);

        var made = await custody.CreateInRoomAsync(
            (RoomId)ROOM,
            OWNER,
            [
                new FurniToPlace(
                    new FurniToMake(CHAIR, "{\"note\":1}"),
                    new RoomPosition(3, 4, 1.5, Rotation.East, 12)
                ),
            ],
            cause,
            Ct
        );

        var row = _world.Row(made.Single().Id);
        (row.RoomEntityId, row.PlayerEntityId, row.X, row.Y, row.Z, row.Rotation, row.WallOffset)
            .Should()
            .Be((ROOM, OWNER, 3, 4, 1.5, Rotation.East, 12));
        var movement = _world.Movements().Should().ContainSingle().Subject;
        (
            movement.Cause,
            movement.Actor,
            movement.FromKind,
            movement.ToKind,
            movement.ToOwner,
            movement.ToPlace
        )
            .Should()
            .Be(
                (
                    MovementCauses.ROOM_POST_IT,
                    OTHER,
                    PlaceKind.None,
                    PlaceKind.Room,
                    OWNER,
                    (long)ROOM
                )
            );
        (movement.X, movement.Y, movement.WallOffset).Should().Be((3, 4, 12));
        movement.SnapshotExtraData.Should().Be("{\"note\":1}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ADeletedRoomsBackstop_SendsWhatIsLeftHome_RecordingOnlyWhileOn(bool recording)
    {
        _world.AddFurni(10, OTHER, CHAIR, ROOM, x: 2, y: 3);
        _world.Config.Set(new() { Enabled = recording });
        var custody = await CustodyAsync();
        var cause = MovementCause.New(MovementCauses.ROOM_DELETE, 0, ROOM);

        var sent = await InUnitAsync(db =>
            custody.SendHomeLeftInRoomAsync(db, (RoomId)ROOM, cause, Ct)
        );

        sent.Should().Be(2);
        _world.Rows().Should().AllSatisfy(x => x.RoomEntityId.Should().BeNull());

        if (!recording)
        {
            _world.Movements().Should().BeEmpty();

            return;
        }

        _world
            .Movements()
            .Select(x => (x.Seq, x.AssetId, x.FromKind, x.FromPlace, x.X, x.ToKind, x.ToOwner))
            .Should()
            .Equal(
                ((short)0, 10L, PlaceKind.Room, (long)ROOM, (int?)2, PlaceKind.Inventory, OTHER),
                (
                    (short)1,
                    (long)CHEST,
                    PlaceKind.Room,
                    (long)ROOM,
                    (int?)1,
                    PlaceKind.Inventory,
                    OWNER
                )
            );
    }

    [Fact]
    public async Task ABorrowedFurni_IsLentIntoTheRoomFromNowhere_AndGoesBackToNobody()
    {
        var custody = await CustodyAsync();
        var item = Borrowed(70_000_001);
        var borrow = MovementCause.New(MovementCauses.ROOM_BC_BORROW, OTHER, 123);
        var pickup = MovementCause.New(MovementCauses.ROOM_PICKUP, OTHER);

        await custody.BorrowIntoRoomAsync((RoomId)ROOM, item, borrow, Ct);

        BorrowedRows().Should().ContainSingle();

        (
            await InUnitAsync(db =>
                custody.GiveBackBorrowedAsync(
                    db,
                    (RoomId)ROOM,
                    [item, Borrowed(70_000_002)],
                    pickup,
                    Ct
                )
            )
        )
            .Should()
            .Be(1, "the second was never borrowed");

        BorrowedRows().Should().BeEmpty();
        _world
            .Movements()
            .Select(x =>
                (x.Cause, x.AssetKind, x.AssetId, x.FromKind, x.ToKind, x.ToOwner, x.FromOwner)
            )
            .Should()
            .Equal(
                (
                    MovementCauses.ROOM_BC_BORROW,
                    AssetKind.BorrowedFurni,
                    70_000_001L,
                    PlaceKind.None,
                    PlaceKind.Room,
                    0,
                    0
                ),
                (
                    MovementCauses.ROOM_PICKUP,
                    AssetKind.BorrowedFurni,
                    70_000_001L,
                    PlaceKind.Room,
                    PlaceKind.None,
                    0,
                    0
                )
            );
    }

    private async Task<FurnitureCustody> CustodyAsync() =>
        TestCustody.Over(_world.Db, await _world.NewRecorderAsync(Ct));

    /// <summary>Runs a transition in a unit of work of its own, kept only when it says so.</summary>
    private Task<T> InUnitAsync<T>(Func<Turbo.Database.Context.TurboDbContext, Task<T>> work) =>
        _world.Db.InTransactionAsync(
            async db =>
            {
                var result = await work(db);

                return result is false ? UnitOfWork.Rollback(result) : UnitOfWork.Commit(result);
            },
            Ct
        );

    private List<BuildersClubFurnitureEntity> BorrowedRows()
    {
        using var db = _world.Db.CreateDbContext();

        return [.. db.BuildersClubFurnitures.AsNoTracking()];
    }

    private List<FurnitureItemSnapshot> Items(params int[] ids) =>
        [
            .. ids.Select(id => new FurnitureItemSnapshot
            {
                ItemId = id,
                SpriteId = CHAIR,
                OwnerId = OTHER,
                OwnerName = "other",
                Definition = Definition(),
                StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
                ExtraData = "",
                SecondsToExpiration = -1,
                HasRentPeriodStarted = false,
                RoomId = RoomId.Invalid,
            }),
        ];

    private RoomItemSnapshot Borrowed(int objectId) =>
        new RoomFloorItemSnapshot
        {
            ObjectId = objectId,
            OwnerId = OTHER,
            OwnerName = "other",
            DefinitionId = CHAIR,
            SpriteId = CHAIR,
            X = 1,
            Y = 2,
            Z = 0,
            Rotation = Rotation.North,
            StackHeight = 1,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = "",
            UsagePolicy = default,
        };

    private static FurnitureDefinitionSnapshot Definition() =>
        _definition ??= new FurnitureDefinitionEntity
        {
            Id = CHAIR,
            SpriteId = CHAIR,
            Name = "chair",
            ProductType = Turbo.Primitives.Furniture.Enums.ProductType.Floor,
            FurniCategory = Turbo.Primitives.Furniture.Enums.FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        }.ToSnapshot(0.01);

    private static FurnitureDefinitionSnapshot? _definition;
}
