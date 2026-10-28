using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Migrations;
using Turbo.Furniture;
using Turbo.Furniture.Configuration;
using Turbo.Main.Hotel;
using Turbo.Players.Providers;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Database;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// The host hotel API's database reads on a real MySQL or MariaDB, as the hotel runs them: the
/// contract runs them on SQLite, which translates (and escapes) LINQ its own way. Skipped unless
/// <c>TURBO_TEST_MYSQL</c> names a server (see <see cref="MySqlMigrationTests"/>); the class makes,
/// migrates and drops a database of its own.
/// </summary>
public sealed class MySqlHostHotelReadsTests(MySqlHostHotelReadsTests.Database database)
    : IClassFixture<MySqlHostHotelReadsTests.Database>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IHotel Hotel => database.Hotel ?? throw new InvalidOperationException("no server");

    [Fact]
    public async Task Players_are_read_found_named_and_searched_by_their_start()
    {
        database.RequireServer();
        var players = Hotel.Players;

        (await players.GetAsync(new PlayerId(3), Ct))!.Name.Should().Be("Bob");
        (await players.GetAsync(new PlayerId(999), Ct)).Should().BeNull();
        (await players.FindAsync(" ALICE ", Ct))!.Id.Should().Be(new PlayerId(1));
        (await players.FindAsync("nobody", Ct)).Should().BeNull();
        (await players.NamesAsync([new PlayerId(1), new PlayerId(3), new PlayerId(999)], Ct))
            .Should()
            .BeEquivalentTo(
                new Dictionary<PlayerId, string> { [new(1)] = "Alice", [new(3)] = "Bob" }
            );

        (await players.SearchAsync("AL", 10, Ct))
            .Select(x => x.Name)
            .Should()
            .Equal("al_x", "alfred", "Alice", "alxy");
        (await players.SearchAsync("al", 2, Ct))
            .Select(x => x.Name)
            .Should()
            .Equal("al_x", "alfred");
        (await players.SearchAsync("al", 500, Ct)).Should().HaveCount(4);
        // A LIKE wildcard in what is typed is a character of the name, not a pattern.
        (await players.SearchAsync("al_", 10, Ct))
            .Select(x => x.Name)
            .Should()
            .Equal("al_x");
        (await players.SearchAsync("%", 10, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Toplists_rank_each_stat_page_by_page_without_system_accounts()
    {
        database.RequireServer();
        var players = Hotel.Players;

        (await players.TopByAsync(PlayerStat.Respects, 2, 0, Ct))
            .Select(x => (x.Rank, x.Player.Value, x.Name, x.Value))
            .Should()
            .Equal((1, 2, "alfred", 9L), (2, 3, "Bob", 7L));
        (await players.TopByAsync(PlayerStat.Respects, 2, 2, Ct))
            .Select(x => (x.Rank, x.Player.Value, x.Value))
            .Should()
            .Equal((3, 1, 5L), (4, 5, 0L));

        (await players.TopByAsync(PlayerStat.AchievementScore, 10, 0, Ct))
            .Select(x => (x.Rank, x.Player.Value, x.Value))
            .Should()
            .Equal((1, 3, 300L), (2, 1, 120L));
        (await players.TopByAsync(PlayerStat.AchievementScore, 1, 1, Ct))
            .Select(x => (x.Rank, x.Player.Value))
            .Should()
            .Equal((2, 1));

        (await players.TopByAsync(PlayerStat.Balance("credits"), 10, 0, Ct))
            .Select(x => (x.Rank, x.Player.Value, x.Value))
            .Should()
            .Equal((1, 1, 500L), (2, 3, 40L));
        (await players.TopByAsync(PlayerStat.Balance("diamonds"), 1, 1, Ct))
            .Select(x => (x.Rank, x.Player.Value, x.Value))
            .Should()
            .Equal((2, 1, 2L));
        (await players.TopByAsync(PlayerStat.Balance("beans"), 10, 0, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_room_is_read_with_its_owner_and_furni_is_counted_without_system_accounts()
    {
        database.RequireServer();

        var room = await Hotel.Rooms.GetAsync(new RoomId(1), Ct);

        room.Should()
            .BeEquivalentTo(
                new HotelRoom(new RoomId(1), "Den", string.Empty, new PlayerId(3), "Bob", false)
            );
        (await Hotel.Rooms.GetAsync(new RoomId(999), Ct)).Should().BeNull();
        (await Hotel.Catalog.CirculationAsync([10, 11, 999], Ct))
            .Should()
            .BeEquivalentTo(new Dictionary<int, int> { [10] = 2, [11] = 1 });
    }

    /// <summary>One migrated database for the class, seeded once; the reads change nothing.</summary>
    public sealed class Database : IAsyncLifetime
    {
        private static readonly string? SERVER = Environment.GetEnvironmentVariable(
            "TURBO_TEST_MYSQL"
        );

        private readonly string _name = "turbo_test_" + Guid.NewGuid().ToString("N")[..12];

        public IHotel? Hotel { get; private set; }

        public void RequireServer() =>
            Assert.SkipUnless(SERVER is not null, "TURBO_TEST_MYSQL is not set");

        public async ValueTask InitializeAsync()
        {
            if (SERVER is null)
                return;

            var ct = TestContext.Current.CancellationToken;
            var factory = new PooledDbContextFactory<TurboDbContext>(ContextOptions());

            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                await new DatabaseMigrator(
                    Options.Create(new DatabaseConfig { Migrate = MigrationMode.Auto }),
                    new MySqlMigrationLock(),
                    new CapturingLogger<DatabaseMigrator>()
                ).MigrateAsync(db, "core", null, ct);
            }

            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                Seed(db);
                await db.SaveChangesAsync(ct);
            }

            var currencies = new CurrencyTypeProvider(
                factory,
                NullLogger<ICurrencyTypeProvider>.Instance
            );
            var definitions = new FurnitureDefinitionProvider(
                Options.Create(new FurnitureConfig()),
                factory,
                NullLogger<IFurnitureDefinitionProvider>.Instance
            );

            await currencies.ReloadAsync(ct);
            await definitions.ReloadAsync(ct);

            var fakes = new Fakes();

            Hotel = new ServiceCollection()
                .AddSingleton<IDbContextFactory<TurboDbContext>>(factory)
                .AddSingleton(fakes.Create<IGrainFactory>())
                .AddSingleton(fakes.Create<ISessionGateway>())
                .AddSingleton(fakes.Create<INavigatorService>())
                .AddSingleton(fakes.Create<IPlayerNoticeService>())
                .AddSingleton(fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>())
                .AddSingleton(fakes.Create<ICatalogEditService>())
                .AddSingleton(fakes.Create<IExpiringPageService>())
                .AddSingleton<ICurrencyTypeProvider>(currencies)
                .AddSingleton<IFurnitureDefinitionProvider>(definitions)
                .AddSingleton(new TestEventBus().System)
                .AddSingleton(fakes.Create<Turbo.Primitives.Navigator.IPublicRoomEditor>())
                .AddSingleton(fakes.Create<Turbo.Primitives.Rooms.IRoomService>())
                .AddSingleton<Turbo.Admin.Commands.AdminActionLog>()
                .AddLogging()
                .AddTurboHotel()
                .BuildServiceProvider()
                .GetRequiredService<IHotel>();
        }

        public async ValueTask DisposeAsync()
        {
            if (SERVER is null)
                return;

            await using var connection = new MySqlConnection(SERVER);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_name}`";
            await command.ExecuteNonQueryAsync();
        }

        private DbContextOptions<TurboDbContext> ContextOptions() =>
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(
                    new MySqlConnectionStringBuilder(SERVER!) { Database = _name }.ConnectionString,
                    ServerVersion.AutoDetect(SERVER!),
                    mysql => mysql.MigrationsAssembly("Turbo.Database")
                )
                .Options;

        // Players 1-5 and a system account (6) that outranks them all in every stat.
        private static void Seed(TurboDbContext db)
        {
            Player(db, 1, "Alice", respects: 5);
            Player(db, 2, "alfred", respects: 9);
            Player(db, 3, "Bob", respects: 7);
            Player(db, 4, "alxy", respects: -1);
            Player(db, 5, "al_x", respects: 0);
            Player(db, 6, "System", respects: 100, system: true);

            db.CurrencyTypes.AddRange(
                Currency(1, "credits", CurrencyType.Credits, null),
                Currency(2, "duckets", CurrencyType.ActivityPoints, 0),
                Currency(3, "diamonds", CurrencyType.ActivityPoints, 5)
            );
            db.PlayerCurrencies.AddRange(
                Balance(1, 1, 500),
                Balance(3, 1, 40),
                Balance(6, 1, 1_000_000),
                Balance(3, 3, 9),
                Balance(1, 3, 2),
                Balance(6, 3, 99)
            );

            db.AchievementProgress.AddRange(
                Score(1, 1, 100),
                Score(1, 2, 20),
                Score(3, 1, 300),
                Score(6, 1, 5000)
            );

            db.RoomModels.Add(
                new RoomModelEntity
                {
                    Id = 1,
                    Name = "model",
                    Model = "00\r00",
                    DoorX = 0,
                    DoorY = 0,
                    DoorRotation = Rotation.North,
                    Enabled = true,
                    Custom = false,
                }
            );
            db.Rooms.Add(
                new RoomEntity
                {
                    Id = 1,
                    Name = "Den",
                    PlayerEntityId = 3,
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    UsersNow = 0,
                    PlayersMax = 25,
                    WallHeight = -1,
                    HideWalls = false,
                    ThicknessWall = RoomThicknessType.Normal,
                    ThicknessFloor = RoomThicknessType.Normal,
                    AllowBlocking = false,
                    AllowPets = true,
                    AllowPetsEat = true,
                    TradeType = RoomTradeModeType.Disabled,
                    MuteType = ModSettingType.Owner,
                    KickType = ModSettingType.Owner,
                    BanType = ModSettingType.Owner,
                    ChatFloodType = ChatFloodSensitivityType.Minimal,
                    PlayerEntity = null!,
                    RoomModelEntity = null!,
                }
            );

            db.FurnitureDefinitions.AddRange(Definition(10, "throne"), Definition(11, "rare"));
            db.Furnitures.AddRange(
                Furni(1, 1, 10),
                Furni(2, 3, 10),
                Furni(3, 6, 10),
                Furni(4, 1, 11)
            );
        }

        private static void Player(
            TurboDbContext db,
            int id,
            string name,
            int respects,
            bool system = false
        ) =>
            db.Players.Add(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                    IsSystemAccount = system,
                    RespectPoints = respects,
                }
            );

        private static CurrencyTypeEntity Currency(
            int id,
            string name,
            CurrencyType type,
            int? pointType
        ) =>
            new()
            {
                Id = id,
                Name = name,
                CurrencyType = type,
                ActivityPointType = pointType,
                Enabled = true,
            };

        private static PlayerCurrencyEntity Balance(int player, int currency, int amount) =>
            new()
            {
                PlayerEntityId = player,
                CurrencyTypeEntityId = currency,
                Amount = amount,
            };

        private static AchievementProgressEntity Score(int player, int achievement, int score) =>
            new()
            {
                PlayerId = player,
                AchievementId = achievement,
                ScoreEarned = score,
            };

        private static FurnitureDefinitionEntity Definition(int id, string name) =>
            new()
            {
                Id = id,
                SpriteId = id,
                Name = name,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                Logic = "default",
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
            };

        private static FurnitureEntity Furni(int id, int owner, int definition) =>
            new()
            {
                Id = id,
                PlayerEntityId = owner,
                FurnitureDefinitionEntityId = definition,
            };
    }
}
