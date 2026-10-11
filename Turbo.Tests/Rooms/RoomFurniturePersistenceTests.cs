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
    public async Task A_pickup_whose_first_write_fails_is_written_by_the_next_flush()
    {
        var db = new FailingOnceDb();
        await using (var ctx = db.Inner.CreateDbContext())
        {
            ctx.Furnitures.Add(
                new FurnitureEntity
                {
                    Id = ITEM,
                    PlayerEntityId = OWNER,
                    FurnitureDefinitionEntityId = 1,
                    RoomEntityId = ROOM,
                    X = 1,
                    Y = 1,
                }
            );
            await ctx.SaveChangesAsync(Ct);
        }

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyItemAsync(
            ROOM,
            Item(x: 4, y: 5),
            Ct,
            remove: true
        );

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);
        row.RoomEntityId.Should().BeNull("the owner picked it up, so it is in their inventory");
    }

    [Fact]
    public async Task A_move_whose_first_write_fails_is_written_by_the_next_flush()
    {
        var db = new FailingOnceDb();
        await using (var ctx = db.Inner.CreateDbContext())
        {
            ctx.Furnitures.Add(
                new FurnitureEntity
                {
                    Id = ITEM,
                    PlayerEntityId = OWNER,
                    FurnitureDefinitionEntityId = 1,
                    RoomEntityId = ROOM,
                    X = 1,
                    Y = 1,
                }
            );
            await ctx.SaveChangesAsync(Ct);
        }

        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueDirtyItemsAsync(ROOM, [Item(x: 4, y: 5)], Ct);

        await FlushItems(grain);
        await FlushItems(grain);

        await using var read = db.Inner.CreateDbContext();
        var row = await read.Furnitures.SingleAsync(x => x.Id == ITEM, Ct);
        (row.RoomEntityId, row.X, row.Y).Should().Be((ROOM, 4, 5));
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
        await using (var ctx = db.Inner.CreateDbContext())
        {
            ctx.Bots.Add(bot);
            await ctx.SaveChangesAsync(Ct);
        }

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

        public InMemoryDb Inner { get; } = new();

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
