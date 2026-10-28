using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Movements;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Trading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>Atomic custody behavior against real MySQL, skipped unless TURBO_TEST_MYSQL is set.</summary>
public sealed class MySqlFurnitureTradeTests
{
    private const int FIRST = 81;
    private const int SECOND = 82;
    private const int DEFINITION = 7;

    private static readonly string? SERVER = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SameOperationConcurrentRequestsReplayOneCommit()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var store = await MySqlTradeStore.CreateAsync([barrier]);
        await store.SeedAsync();
        var custody = await store.NewCustodyAsync();
        var request = store.Request();
        var cause = MovementCause.New(MovementCauses.TRADE, SECOND);

        var results = await Task.WhenAll(
            custody.ExchangeTradeAsync(request, cause, Ct),
            custody.ExchangeTradeAsync(request, cause, Ct)
        );

        barrier.WaitingInserts.Should().Be(2, "both requests passed the initial receipt lookup");
        results.Should().OnlyContain(x => x);
        await using var db = await store.Factory.CreateDbContextAsync(Ct);
        (await db.FurnitureTradeReceipts.CountAsync(x => x.OperationId == cause.OperationId, Ct))
            .Should()
            .Be(1);
        (await db.Furnitures.OrderBy(x => x.Id).Select(x => x.PlayerEntityId).ToListAsync(Ct))
            .Should()
            .Equal(SECOND, FIRST);
        (await db.AssetMovementOutbox.CountAsync(x => x.OperationId == cause.OperationId, Ct))
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task OppositeTradesOverlappingRowsHaveOneAtomicWinner()
    {
        await using var store = await MySqlTradeStore.CreateAsync();
        await store.SeedAsync();
        var custody = await store.NewCustodyAsync();
        var forward = store.Request();
        var reverse = new FurnitureTradeRequest
        {
            InitiatorId = new(SECOND),
            PartnerId = new(FIRST),
            InitiatorItems = [forward.PartnerItems.Single() with { OwnerId = new(SECOND) }],
            PartnerItems = [forward.InitiatorItems.Single() with { OwnerId = new(FIRST) }],
        };

        var results = await Task.WhenAll(
            custody.ExchangeTradeAsync(forward, MovementCause.New(MovementCauses.TRADE, FIRST), Ct),
            custody.ExchangeTradeAsync(reverse, MovementCause.New(MovementCauses.TRADE, SECOND), Ct)
        );

        results.Count(x => x).Should().Be(1);
        await using var db = await store.Factory.CreateDbContextAsync(Ct);
        (await db.Furnitures.OrderBy(x => x.Id).Select(x => x.PlayerEntityId).ToListAsync(Ct))
            .Should()
            .Equal([SECOND, FIRST], "exactly one full exchange committed");
        (await db.FurnitureTradeReceipts.CountAsync(Ct)).Should().Be(1);
        (await db.AssetMovementOutbox.CountAsync(Ct))
            .Should()
            .Be(3, "one start marker and two trade movements, with no second exchange");
    }

    [Fact]
    public async Task ExtraDataComparisonIsOrdinalDespiteMySqlCaseInsensitiveCollation()
    {
        await using var store = await MySqlTradeStore.CreateAsync();
        await store.SeedAsync(firstExtraData: "CaseSensitive");
        var custody = await store.NewCustodyAsync();
        var request = store.Request(firstExtraData: "casesensitive");
        var cause = MovementCause.New(MovementCauses.TRADE, SECOND);

        (await custody.ExchangeTradeAsync(request, cause, Ct)).Should().BeFalse();

        await using var db = await store.Factory.CreateDbContextAsync(Ct);
        (await db.FurnitureTradeReceipts.CountAsync(Ct)).Should().Be(0);
        (await db.AssetMovementOutbox.CountAsync(x => x.OperationId == cause.OperationId, Ct))
            .Should()
            .Be(0);
        (await db.Furnitures.OrderBy(x => x.Id).Select(x => x.PlayerEntityId).ToListAsync(Ct))
            .Should()
            .Equal(FIRST, SECOND);
    }

    [Fact]
    public async Task CurrentCanTradeDefinitionIsCheckedInTheWritePredicate()
    {
        await using var store = await MySqlTradeStore.CreateAsync();
        await store.SeedAsync();
        await using (var db = await store.Factory.CreateDbContextAsync(Ct))
        {
            await db
                .FurnitureDefinitions.Where(x => x.Id == DEFINITION)
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.CanTrade, false), Ct);
        }
        var custody = await store.NewCustodyAsync();
        var cause = MovementCause.New(MovementCauses.TRADE, SECOND);

        (await custody.ExchangeTradeAsync(store.Request(), cause, Ct)).Should().BeFalse();

        await using var read = await store.Factory.CreateDbContextAsync(Ct);
        (await read.FurnitureTradeReceipts.CountAsync(Ct)).Should().Be(0);
        (await read.Furnitures.OrderBy(x => x.Id).Select(x => x.PlayerEntityId).ToListAsync(Ct))
            .Should()
            .Equal(FIRST, SECOND);
    }

    [Fact]
    public async Task A_present_locked_by_an_exchange_cannot_be_opened_by_its_former_owner()
    {
        var pause = new PresentExchangePause();
        await using var store = await MySqlTradeStore.CreateAsync(pause);
        await store.SeedAsync();
        await using (var db = await store.Factory.CreateDbContextAsync(Ct))
        {
            db.Furnitures.Add(
                new FurnitureEntity
                {
                    Id = 303,
                    PlayerEntityId = SECOND,
                    FurnitureDefinitionEntityId = DEFINITION,
                    ChestItemEntityId = 202,
                }
            );
            await db.SaveChangesAsync(Ct);
        }
        var custody = await store.NewCustodyAsync();
        pause.Enabled = true;
        var exchange = custody.ExchangeTradeAsync(
            store.Request(),
            MovementCause.New(MovementCauses.TRADE, FIRST),
            Ct
        );
        Task<CustodyFurni?>? opening = null;
        try
        {
            await pause.ExchangeLocked.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            opening = custody.TakeOutOfPresentAsync(
                SECOND,
                202,
                MovementCause.New(MovementCauses.USE_PRESENT, SECOND),
                Ct
            );
            await pause.OpeningStarted.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        }
        finally
        {
            pause.ReleaseExchange.TrySetResult();
            (await exchange).Should().BeTrue();
        }

        (await opening!)
            .Should()
            .BeNull("the exchange acquired the present before opening started");
        await using (var read = await store.Factory.CreateDbContextAsync(Ct))
            (await read.Furnitures.SingleAsync(x => x.Id == 303, Ct))
                .ChestItemEntityId.Should()
                .Be(202);
        var opened = await custody.TakeOutOfPresentAsync(
            FIRST,
            202,
            MovementCause.New(MovementCauses.USE_PRESENT, FIRST),
            Ct
        );
        opened.Should().NotBeNull();
        (
            await custody.TakeOutOfPresentAsync(
                FIRST,
                202,
                MovementCause.New(MovementCauses.USE_PRESENT, FIRST),
                Ct
            )
        )
            .Should()
            .BeNull();
        await using var final = await store.Factory.CreateDbContextAsync(Ct);
        (await final.Furnitures.SingleAsync(x => x.Id == 303, Ct))
            .PlayerEntityId.Should()
            .Be(FIRST);
    }

    private sealed class PresentExchangePause : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public TaskCompletionSource ExchangeLocked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OpeningStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseExchange { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                Enabled
                && (
                    command.CommandText.StartsWith(
                        "SELECT * FROM furniture WHERE id =",
                        StringComparison.Ordinal
                    ) || command.CommandText.Contains("`chest_item_id` =", StringComparison.Ordinal)
                )
            )
                OpeningStarted.TrySetResult();
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                Enabled
                && command.CommandText.StartsWith(
                    "SELECT * FROM furniture WHERE id IN",
                    StringComparison.Ordinal
                )
            )
            {
                ExchangeLocked.TrySetResult();
                await ReleaseExchange.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private sealed class MySqlTradeStore : IAsyncDisposable
    {
        private readonly string _database =
            "turbo_trade_test_" + Guid.NewGuid().ToString("N")[..12];
        private readonly List<AssetMovementRecorder> _recorders = [];
        private readonly string _connectionString;

        private MySqlTradeStore(params IInterceptor[] interceptors)
        {
            _connectionString = new MySqlConnectionStringBuilder(SERVER!)
            {
                Database = _database,
            }.ConnectionString;
            Factory = new MySqlTradeDbContextFactory(_connectionString, interceptors);
        }

        public MySqlTradeDbContextFactory Factory { get; }

        public static async Task<MySqlTradeStore> CreateAsync(params IInterceptor[] interceptors)
        {
            Assert.SkipUnless(SERVER is not null, "TURBO_TEST_MYSQL is not set");
            var store = new MySqlTradeStore(interceptors);
            await using (var connection = new MySqlConnection(SERVER!))
            {
                await connection.OpenAsync(Ct);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{store._database}`";
                await command.ExecuteNonQueryAsync(Ct);
            }

            try
            {
                await using var db = await store.Factory.CreateDbContextAsync(Ct);
                await db.Database.EnsureCreatedAsync(Ct);
                return store;
            }
            catch (Exception creationError)
            {
                try
                {
                    await store.DisposeAsync();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(creationError, cleanupError);
                }
                throw;
            }
        }

        public async Task SeedAsync(string? firstExtraData = null)
        {
            await using var db = await Factory.CreateDbContextAsync(Ct);
            db.Players.Add(Player(FIRST, "tradefirst"));
            db.Players.Add(Player(SECOND, "tradesecond"));
            db.FurnitureDefinitions.Add(
                new FurnitureDefinitionEntity
                {
                    Id = DEFINITION,
                    SpriteId = DEFINITION,
                    Name = "trade_test_chair",
                    ProductType = ProductType.Floor,
                    FurniCategory = FurnitureCategory.Default,
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
                }
            );
            db.Furnitures.Add(Furniture(101, FIRST, firstExtraData));
            db.Furnitures.Add(Furniture(202, SECOND, null));
            await db.SaveChangesAsync(Ct);
        }

        public FurnitureTradeRequest Request(string? firstExtraData = null) =>
            new()
            {
                InitiatorId = new(FIRST),
                PartnerId = new(SECOND),
                InitiatorItems = [Snapshot(FIRST, 101, firstExtraData)],
                PartnerItems = [Snapshot(SECOND, 202, null)],
            };

        public async Task<FurnitureCustody> NewCustodyAsync()
        {
            var config = new TestOptionsMonitor<MovementsConfig>(new() { Enabled = true });
            var recorder = new AssetMovementRecorder(
                config,
                Factory,
                NullLogger<AssetMovementRecorder>.Instance
            );
            await recorder.StartAsync(Ct);
            _recorders.Add(recorder);
            return new FurnitureCustody(Factory, recorder, NullLogger<FurnitureCustody>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var recorder in _recorders)
            {
                await recorder.StopAsync(CancellationToken.None);
                recorder.Dispose();
            }
            await using var connection = new MySqlConnection(SERVER!);
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_database}`";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        private static PlayerEntity Player(int id, string name) =>
            new()
            {
                Id = id,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            };

        private static FurnitureEntity Furniture(int id, int owner, string? extraData) =>
            new()
            {
                Id = id,
                PlayerEntityId = owner,
                FurnitureDefinitionEntityId = DEFINITION,
                ExtraData = extraData,
            };

        private static FurnitureItemSnapshot Snapshot(int owner, int id, string? extraData) =>
            new()
            {
                ItemId = new RoomObjectId(id),
                SpriteId = DEFINITION,
                OwnerId = new PlayerId(owner),
                OwnerName = owner == FIRST ? "tradefirst" : "tradesecond",
                Definition = new FurnitureDefinitionSnapshot
                {
                    Id = DEFINITION,
                    SpriteId = DEFINITION,
                    Name = "trade_test_chair",
                    ProductType = ProductType.Floor,
                    FurniCategory = FurnitureCategory.Default,
                    LogicName = "default_floor",
                    TotalStates = 0,
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
                    UsagePolicy = FurnitureUsageType.Nobody,
                    ExtraData = null,
                },
                StuffData = new EmptyStuffSnapshot { StuffBitmask = 0 },
                ExtraData = extraData ?? "{}",
                SecondsToExpiration = -1,
                HasRentPeriodStarted = false,
                RoomId = 0,
            };
    }

    private sealed class MySqlTradeDbContextFactory(
        string connectionString,
        IInterceptor[] interceptors
    ) : IDbContextFactory<TurboDbContext>
    {
        private readonly DbContextOptions<TurboDbContext> _options =
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(connectionString, ServerVersion.AutoDetect(SERVER!))
                .AddInterceptors(interceptors)
                .Options;

        public TurboDbContext CreateDbContext() => new(_options);

        public Task<TurboDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new TurboDbContext(_options));
    }

    private sealed class ReceiptInsertBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothArrived = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private int _waitingInserts;

        public int WaitingInserts => Volatile.Read(ref _waitingInserts);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                eventData
                    .Context?.ChangeTracker.Entries<FurnitureTradeReceiptEntity>()
                    .Any(x => x.State == EntityState.Added) == true
            )
            {
                if (Interlocked.Increment(ref _waitingInserts) == 2)
                    _bothArrived.TrySetResult();
                await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }

            return result;
        }
    }
}
