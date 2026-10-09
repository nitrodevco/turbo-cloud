using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Migrations;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// The migrator against a real MySQL or MariaDB: the real lock between real sessions, and the
/// emulator's own 47 migrations applied to an empty database. Skipped unless
/// <c>TURBO_TEST_MYSQL</c> names a server with rights to create databases, without a database
/// name, for example <c>server=127.0.0.1;port=3307;user=root;password=...</c>. Each test makes
/// and drops a database of its own.
/// </summary>
public sealed class MySqlMigrationTests : IAsyncLifetime
{
    private static readonly string? SERVER = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");

    private readonly string _database = "turbo_test_" + Guid.NewGuid().ToString("N")[..12];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ConnectionString =>
        new MySqlConnectionStringBuilder(SERVER!) { Database = _database }.ConnectionString;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (SERVER is null)
            return;

        await using var connection = new MySqlConnection(SERVER);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{_database}`";
        await command.ExecuteNonQueryAsync();
    }

    private static void RequireServer() =>
        Assert.SkipUnless(SERVER is not null, "TURBO_TEST_MYSQL is not set");

    private TurboDbContext Context(string? database = null)
    {
        var connectionString = database is null
            ? ConnectionString
            : new MySqlConnectionStringBuilder(SERVER!) { Database = database }.ConnectionString;

        var options = new DbContextOptionsBuilder<TurboDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.AutoDetect(SERVER!),
                mysql => mysql.MigrationsAssembly("Turbo.Database")
            )
            .Options;

        return new TurboDbContext(options);
    }

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
                    // The emulator's history has drops from before this feature; a database
                    // that is created has nothing to lose, so none of these tests need them.
                    AllowDestructiveMigrations = false,
                }
            ),
            new MySqlMigrationLock(),
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
        var held = await new MySqlMigrationLock().AcquireAsync(
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
        var migrationLock = new MySqlMigrationLock();
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
        var migrationLock = new MySqlMigrationLock();
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
            await using var connection = new MySqlConnection(SERVER);
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{other}`";
            await command.ExecuteNonQueryAsync(Ct);
        }
    }

    [Fact]
    public async Task ADatabaseAheadOfTheCode_IsRefused_AndIsNotChanged()
    {
        RequireServer();
        await using var db = Context();
        await Migrator().MigrateAsync(db, "core", null, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('29991231000000_FromTheFuture', '9.0.0')",
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
        var unreachable = new MySqlConnectionStringBuilder(SERVER!)
        {
            Port = 1,
            Database = _database,
            ConnectionTimeout = 2,
        }.ConnectionString;

        var act = () =>
            new MySqlMigrationLock().AcquireAsync(unreachable, TimeSpan.FromSeconds(1), Ct);

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
        await db.Database.MigrateAsync(known[20], Ct);

        var result = await Migrator()
            .MigrateAsync(db, "core", new MigrationRunOptions { AllowDestructive = true }, Ct);

        result.Applied.Should().Equal(known.Skip(21));
    }

    [Fact]
    public async Task MonsterplantsWithoutABody_GetOneOfTheTwelve_InTheirOwnPalette()
    {
        RequireServer();
        await using var db = Context();
        await db.Database.MigrateAsync("20261007200000_MapBadgeDisplayLogic", Ct);
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0", Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO `pets` (`id`, `player_id`, `name`, `type_id`, `palette_id`, `breed_id`, `color`, `custom_parts`, `watered_at`) VALUES "
                + "(13, 1, 'old plant', 16, 4, 4, 'FFFFFF', NULL, NOW()), "
                + "(24, 1, 'empty plant', 16, 7, 7, 'FFFFFF', '', NOW()), "
                + "(30, 1, 'shaped plant', 16, 2, 2, 'FFFFFF', '1 9 2', NOW()), "
                + "(41, 1, 'dog', 0, 3, 3, 'FFFFFF', NULL, NOW())",
            Ct
        );

        await Migrator().MigrateAsync(db, "core", null, Ct);

        var parts = await db
            .Pets.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.CustomParts)
            .ToListAsync(Ct);

        parts.Should().Equal("1 2 4", "1 1 7", "1 9 2", null);
    }

    [Fact]
    public async Task CfhTopics_AreTheEvidencedSet_AndATopicAHotelChangedIsKept()
    {
        RequireServer();
        await using var db = Context();
        await db.Database.MigrateAsync("20261009114100_AddCfhReportSources", Ct);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE `cfh_topics` SET `enabled` = 0 WHERE `id` = 14",
            Ct
        );

        await Migrator().MigrateAsync(db, "core", null, Ct);

        var topics = await db.CfhTopics.AsNoTracking().OrderBy(x => x.Id).ToListAsync(Ct);

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
        topics.Select(x => x.Name).Should().OnlyHaveUniqueItems();
        topics
            .Where(x => x.Id != 14)
            .Select(x => (x.Id, x.Name, x.Consequence))
            .Should()
            .Contain((12, "bullying", ""))
            .And.Contain((13, "habbo_name", ""))
            .And.Contain((34, "inappropiate_room_group_event", ""))
            .And.Contain((39, "topic_39", ""));
        topics
            .Where(x => x.Id != 14)
            .Should()
            .OnlyContain(x =>
                x.Consequence == ""
                && (
                    x.Name == "topic_" + x.Id
                    || x.Name == "bullying"
                    || x.Name == "habbo_name"
                    || x.Name == "inappropiate_room_group_event"
                )
            );
        topics.Select(x => x.Id).Should().NotContain([4, 5, 24, 25, 26, 27, 28, 36, 37, 41]);
        topics
            .Single(x => x.Id == 14)
            .Should()
            .Match<Turbo.Database.Entities.Moderation.CfhTopicEntity>(x =>
                x.Name == "swearing" && !x.Enabled
            );
    }
}
