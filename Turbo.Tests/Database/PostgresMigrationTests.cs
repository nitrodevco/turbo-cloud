using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Turbo.Catalog.Providers;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Database.Migrations;
using Turbo.Players.Providers;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// The migrator against a real PostgreSQL: the real lock between real sessions, and the
/// emulator's migrations applied to an empty database. Skipped unless <c>TURBO_TEST_POSTGRES</c>
/// names a server with rights to create databases, for example
/// <c>Host=127.0.0.1;Username=postgres;Password=...</c>. Each test makes and drops a database of
/// its own.
/// </summary>
public sealed class PostgresMigrationTests : IAsyncLifetime
{
    private static readonly string? SERVER = Environment.GetEnvironmentVariable(
        "TURBO_TEST_POSTGRES"
    );

    private readonly string _database = "turbo_test_" + Guid.NewGuid().ToString("N")[..12];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ConnectionString => On(_database);

    private static string On(string database) =>
        new NpgsqlConnectionStringBuilder(SERVER!) { Database = database }.ConnectionString;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (SERVER is null)
            return;

        NpgsqlConnection.ClearAllPools();
        await DropDatabaseAsync(_database);
    }

    private static async Task DropDatabaseAsync(string database)
    {
        await using var connection = new NpgsqlConnection(On("postgres"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static void RequireServer() =>
        Assert.SkipUnless(SERVER is not null, "TURBO_TEST_POSTGRES is not set");

    private TurboDbContext Context(string? database = null) =>
        new(
            (DbContextOptions<TurboDbContext>)
                ServiceCollectionExtensions
                    .UseTurboPostgres(
                        new DbContextOptionsBuilder<TurboDbContext>(),
                        new DatabaseConfig { ConnectionString = On(database ?? _database) },
                        npgsql => npgsql.MigrationsAssembly("Turbo.Database")
                    )
                    .Options
        );

    private static DatabaseMigrator Migrator(
        MigrationMode mode = MigrationMode.Auto,
        int lockSeconds = 30,
        CapturingLogger<DatabaseMigrator>? log = null
    ) =>
        new(
            Options.Create(
                new DatabaseConfig
                {
                    Migrate = mode,
                    MigrationLockSeconds = lockSeconds,
                    AllowDestructiveMigrations = false,
                }
            ),
            new PostgresMigrationLock(),
            log ?? new CapturingLogger<DatabaseMigrator>()
        );

    private static int KnownMigrations(DbContext db) =>
        db.GetService<IMigrationsAssembly>().Migrations.Count;

    [Fact]
    public async Task ADatabaseThatDoesNotExist_IsCreated_AndGetsEveryCoreMigration()
    {
        RequireServer();
        var log = new CapturingLogger<DatabaseMigrator>();
        await using var db = Context();

        var result = await Migrator(log: log).MigrateAsync(db, "core", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.Applied);
        result.Applied.Should().HaveCount(KnownMigrations(db));
        (await db.Database.GetPendingMigrationsAsync(Ct)).Should().BeEmpty();
        log.Entries.Select(x => x.Message)
            .Should()
            .Contain(x => x.Contains("creating the database"));
    }

    [Fact]
    public async Task AMigratedDatabase_IsUpToDateTheSecondTime_AndChecksOut()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);

        var again = await Migrator().MigrateAsync(db, "core", null, Ct);
        var check = await Migrator(MigrationMode.Check).MigrateAsync(db, "core", null, Ct);

        again.Outcome.Should().Be(MigrationOutcome.UpToDate);
        check.Outcome.Should().Be(MigrationOutcome.UpToDate);
    }

    [Fact]
    public async Task CheckMode_RefusesAnEmptyDatabase_AndLeavesItEmpty()
    {
        RequireServer();
        await using var db = Context();

        var act = () => Migrator(MigrationMode.Check).MigrateAsync(db, "core", null, Ct);

        await act.Should().ThrowAsync<MigrationException>().WithMessage("*behind*");
        (
            await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>()
                .ExistsAsync(Ct)
        )
            .Should()
            .BeFalse("checking must not create anything");
    }

    [Fact]
    public async Task TwoServersStartedTogether_TakeTurns_AndEveryMigrationIsAppliedOnce()
    {
        RequireServer();
        await using var first = Context();
        await using var second = Context();

        var results = await Task.WhenAll(
            Migrator().MigrateAsync(first, "core", null, Ct),
            Migrator().MigrateAsync(second, "core", null, Ct)
        );

        results.Sum(x => x.Applied.Count).Should().Be(KnownMigrations(first));
        results.Count(x => x.Outcome == MigrationOutcome.Applied).Should().Be(1);
        results.Count(x => x.Outcome == MigrationOutcome.UpToDate).Should().Be(1);
        (await first.Database.GetAppliedMigrationsAsync(Ct))
            .Should()
            .HaveCount(KnownMigrations(first));
    }

    [Fact]
    public async Task AMigratorWaitsOutALockAnotherSessionHolds_ThenTimesOutWithAReadableMessage()
    {
        RequireServer();
        await using var db = Context();
        var held = await new PostgresMigrationLock().AcquireAsync(
            ConnectionString,
            TimeSpan.FromSeconds(5),
            Ct
        );

        var clock = Stopwatch.StartNew();
        var act = () => Migrator(lockSeconds: 2).MigrateAsync(db, "core", null, Ct);

        await act.Should().ThrowAsync<MigrationException>().WithMessage("*Another process*");
        clock
            .Elapsed.Should()
            .BeGreaterThan(TimeSpan.FromSeconds(1.5))
            .And.BeLessThan(TimeSpan.FromSeconds(15));

        // Nothing was changed while it waited, and once the holder is done the run goes through.
        await held.DisposeAsync();
        (await Migrator().MigrateAsync(db, "core", null, Ct))
            .Outcome.Should()
            .Be(MigrationOutcome.Applied);
    }

    [Fact]
    public async Task TheLockIsFreeAgainTheMomentItsHolderIsGone()
    {
        RequireServer();
        var migrationLock = new PostgresMigrationLock();
        var first = await migrationLock.AcquireAsync(ConnectionString, TimeSpan.FromSeconds(5), Ct);
        await first.DisposeAsync();

        var clock = Stopwatch.StartNew();
        var second = await migrationLock.AcquireAsync(
            ConnectionString,
            TimeSpan.FromSeconds(5),
            Ct
        );
        await second.DisposeAsync();

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ALockHeldOnOneDatabase_DoesNotBlockAnother()
    {
        RequireServer();
        var other = _database + "_b";
        var migrationLock = new PostgresMigrationLock();
        await using var held = await migrationLock.AcquireAsync(
            ConnectionString,
            TimeSpan.FromSeconds(5),
            Ct
        );

        try
        {
            await using var db = Context(other);

            var result = await Migrator(lockSeconds: 2).MigrateAsync(db, "other", null, Ct);

            result.Outcome.Should().Be(MigrationOutcome.Applied);
        }
        finally
        {
            await DropDatabaseAsync(other);
        }
    }

    [Fact]
    public async Task ADatabaseAheadOfTheCode_IsRefused_AndIsNotChanged()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" (migration_id, product_version) VALUES ('29991231000000_FromTheFuture', '10.0.0')",
            Ct
        );

        var act = () => Migrator().MigrateAsync(db, "core", null, Ct);

        await act.Should()
            .ThrowAsync<MigrationException>()
            .WithMessage("*29991231000000_FromTheFuture*newer version*");
    }

    [Fact]
    public async Task AServerThatCannotBeReached_IsReportedAsOneMigrationProblem()
    {
        RequireServer();
        var unreachable = new NpgsqlConnectionStringBuilder(SERVER!)
        {
            Port = 1,
            Database = _database,
            Timeout = 2,
        }.ConnectionString;

        var act = () =>
            new PostgresMigrationLock().AcquireAsync(unreachable, TimeSpan.FromSeconds(1), Ct);

        await act.Should()
            .ThrowAsync<MigrationException>()
            .WithMessage("*Cannot lock the database*");
    }

    [Fact]
    public async Task APartiallyMigratedDatabase_ResumesWhereItStopped()
    {
        RequireServer();
        await using var db = Context();
        var known = db.GetService<IMigrationsAssembly>()
            .Migrations.Keys.Order(StringComparer.Ordinal)
            .ToList();
        await db.Database.MigrateAsync(known[0], Ct);

        var result = await Migrator().MigrateAsync(db, "core", null, Ct);

        result.Applied.Should().Equal(known.Skip(1));
    }

    /// <summary>
    /// The reference rows the old migrations seeded came across with the schema: the
    /// official report window's tree (2026-10-09), in its order.
    /// </summary>
    [Fact]
    public async Task ANewDatabase_HasTheEvidencedCfhTopics_InTheReportWindowsOrder()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);

        var topics = await db.CfhTopics.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(Ct);

        topics
            .Select(x => x.Category)
            .Distinct()
            .Should()
            .BeEquivalentTo(
                "sexual_content",
                "pii_meeting_irl",
                "scamming",
                "trolling_bad_behavior",
                "violent_behavior",
                "game_interruption",
                "unlawful_activity"
            );
        topics
            .Where(x => x.Enabled)
            .Select(x => x.Id)
            .Should()
            .Equal(
                1,
                2,
                3,
                30,
                39,
                6,
                8,
                9,
                10,
                11,
                32,
                33,
                12,
                13,
                34,
                14,
                15,
                16,
                17,
                18,
                19,
                20,
                21,
                22,
                29,
                35,
                40
            );
        topics.Select(x => x.Id).Should().NotContain([4, 5, 24, 25, 26, 27, 28, 36, 37, 41]);
    }

    /// <summary>The shipped pet speech gives each animal its own sounds.</summary>
    [Fact]
    public async Task ANewDatabase_GivesEachAnimalItsOwnSounds()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);

        var lines = await db.PetSpeech.AsNoTracking().ToListAsync(Ct);

        lines
            .Where(x => x.Line == "Woof!")
            .Select(x => x.TypeId)
            .Should()
            .OnlyContain(type => type == 0 || type == 36, "only the dog and the dragon dog bark");
        lines.Where(x => x.TypeId == 1).Select(x => x.Line).Should().Contain("Meow!");
        lines.Should().OnlyContain(x => x.Line.Length > 0 && x.Line.Length <= 100);
    }

    /// <summary>
    /// A new hotel is playable once migrated: a room can be made on any of the standard models,
    /// each opening onto a tile a player can stand on; the catalog has a root to build on and sells
    /// Habbo Club; credits, duckets and diamonds exist; the navigator has its tabs and categories.
    /// </summary>
    [Fact]
    public async Task ANewDatabase_IsAPlayableHotel()
    {
        RequireServer();
        await using (var db = Context())
            await Migrator().MigrateAsync(db, "core", null, Ct);
        var factory = new Factory(this);

        var models = new RoomModelProvider(factory, NullLogger<IRoomModelProvider>.Instance);
        await models.ReloadAsync(Ct);

        await using (var db = Context())
        {
            var ids = await db.RoomModels.Select(x => x.Id).ToListAsync(Ct);

            ids.Should().HaveCount(36);

            foreach (var model in ids.Select(models.GetModelById))
                model
                    .BaseFlags[model.DoorY * model.Width + model.DoorX]
                    .Should()
                    .Be(RoomTileFlags.Open, model.Name + "'s door");

            (await db.NavigatorTopLevelContexts.Select(x => x.SearchCode).ToListAsync(Ct))
                .Should()
                .BeEquivalentTo("official_view", "hotel_view", "roomads_view", "myworld_view");
            (await db.NavigatorFlatCategories.CountAsync(x => !x.StaffOnly, Ct))
                .Should()
                .BeGreaterThan(0);
            (await db.NavigatorEventCategories.CountAsync(Ct)).Should().BeGreaterThan(0);
        }

        var catalog = await new CatalogSnapshotProvider<NormalCatalog>(
            factory,
            NullLogger<ICatalogSnapshotProvider<NormalCatalog>>.Instance,
            new Fakes().Create<IFurnitureDefinitionProvider>(),
            CatalogType.Normal
        ).GetSnapshotAsync(Ct);

        catalog.RootPageId.Should().BePositive();
        catalog
            .OffersById.Values.Select(x => x.LocalizationId)
            .Should()
            .Contain(["habbo_club_1_month", "habbo_club_3_months", "habbo_club_12_months"]);

        var currencies = new CurrencyTypeProvider(
            factory,
            NullLogger<ICurrencyTypeProvider>.Instance
        );
        await currencies.ReloadAsync(Ct);

        currencies
            .GetEnabledCurrencyNames()
            .Should()
            .BeEquivalentTo("credits", "duckets", "diamonds");
        currencies.TryGetCurrencyTypeId(CurrencyKind.Credits, out _).Should().BeTrue();
    }

    /// <summary>
    /// The queries that run on a timer or on every login can each use their index, so their cost
    /// stays flat as the tables grow: the respect and achievement recovery every few seconds, the
    /// fact prune, the web sign-in's sweep, the offline-message replay, and the moderators' chat
    /// search by any part of a line. Each query has the shape of the code that sends it.
    /// </summary>
    [Fact]
    public async Task TheTimerAndLoginQueries_UseTheirIndexes()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);
        var now = DateTime.UtcNow;
        var friends = new List<int> { 2, 3 };

        // A player who has talked a lot and has three messages waiting: the replay should read the
        // three, not the history, and the planner should know there is a history to avoid.
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO players (id, name, name_key, figure, gender, status)
            SELECT g, 'p' || g, 'p' || g, 'hd-180-1', 0, 0 FROM generate_series(1, 3) g;
            INSERT INTO messenger_messages (sender_id, receiver_id, message, sent_at, delivered)
            SELECT 2 + (g % 2), 1, 'hi', now() - g * interval '1 minute', g > 3
            FROM generate_series(1, 20000) g;
            ANALYZE messenger_messages;
            """,
            Ct
        );

        (
            await PlanAsync(
                db,
                db.HumanRespectOperations.Where(x => !x.Completed && !x.Rejected)
                    .OrderBy(x => x.OperationId)
                    .Take(50)
            )
        )
            .Should()
            .Contain("ix_player_respect_operations_operation_id");
        (
            await PlanAsync(
                db,
                db.PetRespectOperations.Where(x => !x.Completed && !x.Rejected)
                    .OrderBy(x => x.OperationId)
                    .Take(50)
            )
        )
            .Should()
            .Contain("ix_pet_respect_operations_operation_id");
        (await PlanAsync(db, db.AchievementFacts.Where(x => !x.Processed).Select(x => x.PlayerId)))
            .Should()
            .Contain("ix_achievement_facts_player_id");
        (
            await PlanAsync(
                db,
                db.AchievementProjections.Where(x => x.PublicationPending).Select(x => x.PlayerId)
            )
        )
            .Should()
            .Contain("ix_achievement_projections_player_id");
        (
            await PlanAsync(
                db,
                db.AchievementFacts.Where(x => x.Processed && x.OccurredAtUtc < now)
                    .Select(x => x.Id)
            )
        )
            .Should()
            .Contain("ix_achievement_facts_occurred_at");
        (await PlanAsync(db, db.WebSessions.Where(x => x.ExpiresAt <= now)))
            .Should()
            .Contain("ix_auth_web_sessions_expires_at");
        (
            await PlanAsync(
                db,
                db.MessengerMessages.Where(x =>
                        x.ReceiverPlayerEntityId == 1
                        && !x.Delivered
                        && friends.Contains(x.SenderPlayerEntityId)
                    )
                    .OrderByDescending(x => x.Timestamp)
                    .ThenByDescending(x => x.Id)
                    .Take(50)
            )
        )
            .Should()
            .Contain("ix_messenger_messages_receiver_id_sent_at");
        // The friend search: a prefix of a name's key, which the C collation lets a b-tree serve
        // as a range; in a language collation the whole index is read and filtered.
        (
            await PlanAsync(
                db,
                db.Players.Where(x =>
                        EF.Functions.Like(
                            x.NameKey,
                            LikePatterns.StartingWith("jo"),
                            LikePatterns.ESCAPE
                        )
                    )
                    .OrderBy(x => x.NameKey)
                    .Take(50)
            )
        )
            .Should()
            .Contain("ix_players_name_key")
            .And.Contain(
                "Index Cond: ((name_key >= ",
                "the prefix is a range of the index, not a filter"
            );
        // The navigator's room search: any part of a name, in any case.
        (
            await PlanAsync(
                db,
                db.Rooms.Where(x =>
                    EF.Functions.Like(
                        x.Name.ToLower(),
                        LikePatterns.Containing("pool"),
                        LikePatterns.ESCAPE
                    )
                )
            )
        )
            .Should()
            .Contain("ix_rooms_name_trgm");
        (
            await PlanAsync(
                db,
                db.Chatlogs.Where(x => EF.Functions.Like(x.Message.ToLower(), "%rude%", "\\"))
            )
        )
            .Should()
            .Contain("ix_room_chat_logs_message_trgm");
    }

    /// <summary>The plan for a query with sequential scans priced out, so any usable index shows.</summary>
    private static async Task<string> PlanAsync<T>(TurboDbContext db, IQueryable<T> query)
    {
        await using var command = query.CreateDbCommand();
        var connection = command.Connection!;

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        await using (var off = connection.CreateCommand())
        {
            off.CommandText = "SET enable_seqscan = off";
            await off.ExecuteNonQueryAsync(Ct);
        }

        command.CommandText = "EXPLAIN " + command.CommandText;
        var plan = new System.Text.StringBuilder();

        await using var reader = await command.ExecuteReaderAsync(Ct);

        while (await reader.ReadAsync(Ct))
            plan.AppendLine(reader.GetString(0));

        return plan.ToString();
    }

    private sealed class Factory(PostgresMigrationTests owner) : IDbContextFactory<TurboDbContext>
    {
        public TurboDbContext CreateDbContext() => owner.Context();
    }

    /// <summary>
    /// A row's updated_at moves on every update, as MySQL's ON UPDATE CURRENT_TIMESTAMP did,
    /// including through a bulk ExecuteUpdate that no SaveChanges hook would see.
    /// </summary>
    [Fact]
    public async Task UpdatedAt_MovesOnEveryUpdate_EvenABulkOne()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO players (name, name_key, figure, gender, status, updated_at) "
                + "VALUES ('alice', 'alice', 'hd-180-1', 0, 0, TIMESTAMPTZ '2020-01-01 00:00:00+00')",
            Ct
        );

        await db.Players.ExecuteUpdateAsync(set => set.SetProperty(x => x.RespectPoints, 5), Ct);

        var updated = await db.Players.AsNoTracking().Select(x => x.UpdatedAt).SingleAsync(Ct);
        updated.Should().BeAfter(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
