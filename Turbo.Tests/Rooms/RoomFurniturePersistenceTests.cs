using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's furni changes are buffered and written by its persistence grain. A write that fails
/// (a deadlock, a timeout, the database restarting) keeps its changes queued for the next tick:
/// what the players did must reach the database eventually, or the room comes back as it was.
/// </summary>
public sealed class RoomFurniturePersistenceTests
{
    private const string GRAIN = "Turbo.Rooms.Grains.RoomPersistenceGrain";
    private const int ROOM = 7;
    private const int ITEM = 500;
    private const int OWNER = 2;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_move_whose_first_write_fails_is_written_by_the_next_flush()
    {
        var db = new FailingOnceDb();
        db.Inner.Insert(Row(ITEM, ROOM));

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyItemsAsync(ROOM, [Item(x: 4, y: 5)], Ct);

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);
        (row.RoomEntityId, row.X, row.Y).Should().Be((ROOM, 4, 5));
    }

    /// <summary>
    /// A move queued before the furni left the room, written after: it went back into the room
    /// the flush belonged to, while its new owner had it in their inventory or another room.
    /// </summary>
    [Fact]
    public async Task A_move_written_after_the_furni_left_the_room_does_not_bring_it_back()
    {
        var db = new FailingOnceDb();
        db.Inner.Insert(Row(ITEM, room: 9));

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyItemsAsync(ROOM, [Item(x: 4, y: 5)], Ct);

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);
        (row.RoomEntityId, row.X, row.Y).Should().Be((9, 1, 1));
    }

    /// <summary>
    /// A furni whose row is gone (deleted by the panel, recycled) was written with the others,
    /// and the missing row failed the whole batch on every flush, so nothing in it was saved.
    /// </summary>
    [Fact]
    public async Task A_furni_whose_row_is_gone_does_not_stop_the_others_being_written()
    {
        var db = new FailingOnceDb();
        db.Inner.Insert(Row(ITEM, ROOM));

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyItemsAsync(
            ROOM,
            [Item(x: 3, y: 3) with { ObjectId = 999 }, Item(x: 4, y: 5)],
            Ct
        );

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);
        (row.X, row.Y).Should().Be((4, 5));
        read.Furnitures.Should().ContainSingle("a missing row is not written back into being");
    }

    [Fact]
    public async Task A_bot_move_whose_first_write_fails_is_written_by_the_next_flush()
    {
        var db = new FailingOnceDb();
        var bot = new BotEntity
        {
            Id = 40,
            PlayerEntityId = OWNER,
            RoomEntityId = ROOM,
            Name = "frank",
            Motto = "",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
        };
        db.Inner.Insert(bot);

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyBotsAsync(
            [bot.ToSnapshot("owner") with { X = 6, Y = 3 }],
            Ct
        );

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Bots.SingleAsync(x => x.Id == 40, Ct);
        (row.X, row.Y).Should().Be((6, 3));
    }

    private static object NewGrain(IDbContextFactory<TurboDbContext> db)
    {
        var grain = GrainHarness.Create(typeof(RoomGrain).Assembly, GRAIN, new Fakes(), db);
        RoomHarness.SetMember(RoomHarness.GetField(grain, "_state")!, "RoomId", RoomId.Parse(ROOM));

        return grain;
    }

    private static Task FlushItems(object grain) =>
        (Task)
            grain
                .GetType()
                .GetMethod(
                    "FlushDirtyItemsAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    [typeof(CancellationToken)]
                )!
                .Invoke(grain, [CancellationToken.None])!;

    private static FurnitureEntity Row(int id, int room) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            FurnitureDefinitionEntityId = 1,
            RoomEntityId = room,
            X = 1,
            Y = 1,
        };

    private static RoomFloorItemSnapshot Item(int x, int y) =>
        new()
        {
            ObjectId = ITEM,
            OwnerId = OWNER,
            OwnerName = "owner",
            DefinitionId = 1,
            SpriteId = 10,
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
    private sealed class FailingOnceDb : IDbContextFactory<TurboDbContext>
    {
        private bool _failed;

        public SqliteDb Inner { get; } = Seeded();

        private static SqliteDb Seeded()
        {
            var db = new SqliteDb();
            db.InsertOwnersAndRooms([OWNER], [ROOM, 9]);

            return db;
        }

        public TurboDbContext CreateDbContext() => Next();

        public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(Next());

        private TurboDbContext Next()
        {
            if (_failed)
                return Inner.CreateDbContext();

            _failed = true;

            throw new InvalidOperationException("db restarting");
        }
    }
}
