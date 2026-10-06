using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Migrations;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// What the migrator does to a database, and what it refuses to: applied in order and told in
/// the log, behind a lock, never against a database a newer version made, never dropping data
/// unasked, and never twice. Run on SQLite with migrations written for the purpose; the lock is
/// played by a fake here and is the real one in <see cref="MySqlMigrationTests"/>.
/// </summary>
public sealed class DatabaseMigratorTests : IDisposable
{
    private const string CREATE = "20260101000001_CreateThings";
    private const string NOTE = "20260101000002_AddExtras";
    private const string DROP = "20260101000003_DropExtras";

    private readonly MigratorHarness _h = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _h.Dispose();

    private List<string> Messages(LogLevel? level = null) =>
        [.. _h.Log.Entries.Where(x => level is null || x.Level == level).Select(x => x.Message)];

    // --- applying ---

    [Fact]
    public async Task ADatabaseWithNothingInIt_GetsEveryMigration_InOrder_AndSaysSo()
    {
        await using var db = _h.Context<ThingsContext>();

        var result = await _h.Migrator().MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.Applied);
        result.Applied.Should().Equal(CREATE, NOTE, DROP);
        result.Current.Should().Be(DROP);
        (await _h.HistoryAsync()).Should().Equal(CREATE, NOTE, DROP);

        var said = Messages(LogLevel.Information);
        said.Should().Contain(x => x.Contains("3 migration(s)") && x.Contains("creating"));
        said.Should()
            .ContainInOrder([
                $"Database things: applying {CREATE} (1/3)",
                $"Database things: applying {NOTE} (2/3)",
                $"Database things: applying {DROP} (3/3)",
            ]);
        said.Last().Should().Contain("up to date at " + DROP).And.Contain("3 migration(s) applied");
    }

    [Fact]
    public async Task ARunWithNothingPending_ChangesNothing_AndTakesNoLock()
    {
        await using var db = _h.Context<ThingsContext>();
        await _h.Migrator().MigrateAsync(db, "things", null, Ct);
        var locks = _h.Lock.Acquired;

        var result = await _h.Migrator().MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.UpToDate);
        result.Applied.Should().BeEmpty();
        _h.Lock.Acquired.Should().Be(locks, "nothing needed locking");
        Messages(LogLevel.Information).Last().Should().Be($"Database things: up to date at {DROP}");
    }

    [Fact]
    public async Task OnlyWhatIsPendingIsApplied()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(NOTE, Ct);

        var result = await _h.Migrator(c => c.AllowDestructive = true)
            .MigrateAsync(db, "things", null, Ct);

        result.Applied.Should().Equal(DROP);
        (await _h.HistoryAsync()).Should().Equal(CREATE, NOTE, DROP);
    }

    [Fact]
    public async Task TheLockIsTakenOnce_ForTheWholeRun_AndGivenUp()
    {
        await using var db = _h.Context<ThingsContext>();

        await _h.Migrator().MigrateAsync(db, "things", null, Ct);

        _h.Lock.Acquired.Should().Be(1);
        _h.Lock.Released.Should().Be(1);
        _h.Lock.LastTimeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task TheLockWaitComesFromTheConfiguration()
    {
        await using var db = _h.Context<ThingsContext>();

        await _h.Migrator(c => c.LockSeconds = 7).MigrateAsync(db, "things", null, Ct);

        _h.Lock.LastTimeout.Should().Be(TimeSpan.FromSeconds(7));
    }

    // --- the modes ---

    [Fact]
    public async Task CheckMode_AppliesNothing_AndNamesWhatIsPending()
    {
        await using var db = _h.Context<ThingsContext>();

        var act = () =>
            _h.Migrator(c => c.Mode = MigrationMode.Check).MigrateAsync(db, "things", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure
            .Message.Should()
            .Contain("3 migration(s) behind")
            .And.Contain(CREATE)
            .And.Contain(DROP);
        failure.Message.Should().Contain("Turbo.Main migrate").And.Contain("Auto");
        (await _h.TablesAsync()).Should().BeEmpty();
        _h.Lock.Acquired.Should().Be(0);
    }

    [Fact]
    public async Task CheckMode_TellsAPluginsOwnerHowItsTablesAreMigrated_NotToRunTheEmulatorsCommand()
    {
        await using var db = _h.Context<ThingsContext>();

        var act = () =>
            _h.Migrator(c => c.Mode = MigrationMode.Check)
                .MigrateAsync(db, "ThingsContext", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure.Message.Should().Contain("migrated when the plugin loads").And.Contain("Auto");
        failure
            .Message.Should()
            .NotContain("run 'Turbo.Main migrate'", "that does only the emulator's tables");
    }

    [Fact]
    public async Task CheckMode_TellsTheEmulatorsOwnerToRunTheMigrateCommand()
    {
        await using var db = _h.Context<ThingsContext>();

        var act = () =>
            _h.Migrator(c => c.Mode = MigrationMode.Check)
                .MigrateAsync(db, DatabaseMigrator.CORE_LABEL, null, Ct);

        (await act.Should().ThrowAsync<MigrationException>())
            .Which.Message.Should()
            .Contain("Run 'Turbo.Main migrate'");
    }

    [Fact]
    public async Task CheckMode_PassesWhenTheDatabaseIsCurrent()
    {
        await using var db = _h.Context<ThingsContext>();
        await _h.Migrator().MigrateAsync(db, "things", null, Ct);

        var result = await _h.Migrator(c => c.Mode = MigrationMode.Check)
            .MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.UpToDate);
    }

    [Fact]
    public async Task OffMode_DoesNotLookAtTheDatabase()
    {
        await using var db = _h.Context<ThingsContext>();

        var result = await _h.Migrator(c => c.Mode = MigrationMode.Off)
            .MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.Skipped);
        (await _h.TablesAsync()).Should().BeEmpty();
        _h.Lock.Acquired.Should().Be(0);
        Messages(LogLevel.Information).Should().ContainSingle().Which.Should().Contain("off");
    }

    [Fact]
    public async Task ARunCanOverrideTheConfiguredMode()
    {
        await using var db = _h.Context<ThingsContext>();

        // `Turbo.Main migrate` is the operator asking, whatever the server would do on its own.
        var result = await _h.Migrator(c => c.Mode = MigrationMode.Off)
            .MigrateAsync(db, "things", new MigrationRunOptions { Mode = MigrationMode.Auto }, Ct);

        result.Outcome.Should().Be(MigrationOutcome.Applied);
    }

    // --- a database from a newer version ---

    [Theory]
    [InlineData(MigrationMode.Auto)]
    [InlineData(MigrationMode.Check)]
    public async Task ADatabaseWithMigrationsThisBuildDoesNotKnow_IsRefused(MigrationMode mode)
    {
        await using var db = _h.Context<ThingsContext>();
        await _h.Migrator().MigrateAsync(db, "things", null, Ct);
        await _h.Run(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20991231000000_FromTheFuture', '9.0.0')"
        );

        var act = () => _h.Migrator(c => c.Mode = mode).MigrateAsync(db, "things", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure.Message.Should().Contain("20991231000000_FromTheFuture");
        failure.Message.Should().Contain("newer version").And.Contain("restore a backup");
    }

    [Fact]
    public async Task ANewerDatabaseIsNotLookedAtWhenMigrationsAreOff()
    {
        await using var db = _h.Context<ThingsContext>();
        await _h.Migrator().MigrateAsync(db, "things", null, Ct);
        await _h.Run(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20991231000000_FromTheFuture', '9.0.0')"
        );

        var result = await _h.Migrator(c => c.Mode = MigrationMode.Off)
            .MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.Skipped);
    }

    [Fact]
    public async Task ANewerDatabaseIsRefusedBeforeTheLockIsTaken_AndNothingIsApplied()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(CREATE, Ct);
        await _h.Run(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20991231000000_FromTheFuture', '9.0.0')"
        );

        var act = () => _h.Migrator().MigrateAsync(db, "things", null, Ct);

        await act.Should().ThrowAsync<MigrationException>();
        _h.Lock.Acquired.Should().Be(0);
        (await _h.HistoryAsync()).Should().NotContain(NOTE);
    }

    // --- destructive migrations ---

    [Fact]
    public async Task AMigrationThatDropsATable_IsRefusedOnADatabaseWithData_AndNamed()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(NOTE, Ct);

        var act = () => _h.Migrator().MigrateAsync(db, "things", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure.Message.Should().Contain(DROP).And.Contain("drops table extras");
        failure.Message.Should().Contain("Back up").And.Contain("AllowDestructiveMigrations");
        (await _h.HistoryAsync()).Should().Equal(CREATE, NOTE);
        (await _h.TablesAsync()).Should().Contain("extras");
        _h.Lock.Acquired.Should()
            .Be(1, "it is found once the lock is held and the state is read again");
        _h.Lock.Released.Should().Be(1);
    }

    [Fact]
    public async Task AMigrationThatDropsAColumn_IsRefusedToo_AndNamed()
    {
        await using var db = _h.Context<ColumnsContext>();
        await db.Database.MigrateAsync("20260101000001_CreateWithNote", Ct);

        var act = () => _h.Migrator().MigrateAsync(db, "columns", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure.Message.Should().Contain("20260101000002_DropNoteColumn");
        failure.Message.Should().Contain("drops column things.note");
    }

    [Fact]
    public async Task ADestructiveMigrationRuns_WhenTheConfigurationAllowsIt_WithAWarning()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(NOTE, Ct);

        var result = await _h.Migrator(c => c.AllowDestructive = true)
            .MigrateAsync(db, "things", null, Ct);

        result.Applied.Should().Equal(DROP);
        Messages(LogLevel.Warning)
            .Should()
            .Contain(x => x.Contains("destructive") && x.Contains("drops table extras"));
    }

    [Fact]
    public async Task ADestructiveMigrationRuns_WhenTheRunAllowsIt()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(NOTE, Ct);

        var result = await _h.Migrator()
            .MigrateAsync(db, "things", new MigrationRunOptions { AllowDestructive = true }, Ct);

        result.Applied.Should().Equal(DROP);
    }

    [Fact]
    public async Task TheRunsChoiceBeatsTheConfiguration()
    {
        await using var db = _h.Context<ThingsContext>();
        await db.Database.MigrateAsync(NOTE, Ct);

        var act = () =>
            _h.Migrator(c => c.AllowDestructive = true)
                .MigrateAsync(
                    db,
                    "things",
                    new MigrationRunOptions { AllowDestructive = false },
                    Ct
                );

        await act.Should().ThrowAsync<MigrationException>();
    }

    [Fact]
    public async Task ADestructiveMigrationIsNotAskedAbout_WhenTheDatabaseIsNew()
    {
        await using var db = _h.Context<ThingsContext>();

        // Creating a database applies the whole history, drop and all; there is nothing to lose.
        var result = await _h.Migrator().MigrateAsync(db, "things", null, Ct);

        result.Applied.Should().HaveCount(3);
        Messages(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task AnExistingDatabaseIsWarnedThatABackupIsTheOnlyWayBack()
    {
        await using var db = _h.Context<LettersContext>();
        await db.Database.MigrateAsync("20260101000001_CreateA", Ct);

        await _h.Migrator().MigrateAsync(db, "letters", null, Ct);

        Messages(LogLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("has data")
            .And.Contain("backup");
    }

    // --- failing, racing, cancelling ---

    [Fact]
    public async Task AFailedMigration_IsNamed_KeepsTheEarlierOnes_AndGivesUpTheLock()
    {
        await using var db = _h.Context<BrokenContext>();

        var act = () => _h.Migrator().MigrateAsync(db, "broken", null, Ct);

        var failure = (await act.Should().ThrowAsync<MigrationException>()).Which;
        failure.Message.Should().Contain("20260101000002_ThisIsNotSql").And.Contain("failed");
        failure.Message.Should().Contain("1 earlier migration(s) were applied");
        failure.InnerException.Should().NotBeNull();
        (await _h.HistoryAsync()).Should().Equal("20260101000001_CreateFirst");
        _h.Lock.Released.Should().Be(1);
    }

    [Fact]
    public async Task AMigrationAnotherProcessAppliedWhileWaitingForTheLock_IsNotAppliedTwice()
    {
        await using var db = _h.Context<ThingsContext>();
        await using var other = _h.Context<ThingsContext>();
        _h.Lock.WhileWaiting = () => other.Database.MigrateAsync(DROP, Ct);

        var result = await _h.Migrator(c => c.AllowDestructive = true)
            .MigrateAsync(db, "things", null, Ct);

        result.Outcome.Should().Be(MigrationOutcome.UpToDate);
        result.Applied.Should().BeEmpty();
        (await _h.HistoryAsync()).Should().Equal(CREATE, NOTE, DROP);
        _h.Lock.Released.Should().Be(1);
    }

    [Fact]
    public async Task ALockThatStaysHeld_FailsTheRunWithItsOwnMessage_AndChangesNothing()
    {
        await using var db = _h.Context<ThingsContext>();
        _h.Lock.Fail = new MigrationException("Another process has been migrating for 60 seconds.");

        var act = () => _h.Migrator().MigrateAsync(db, "things", null, Ct);

        await act.Should().ThrowAsync<MigrationException>().WithMessage("*Another process*");
        (await _h.TablesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ACancellationWhileTheLockIsHeld_IsNotDressedUpAsAMigrationFailure()
    {
        await using var db = _h.Context<ThingsContext>();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _h.Lock.WhileWaiting = () =>
        {
            cancel.Cancel();

            return Task.CompletedTask;
        };

        var act = () => _h.Migrator().MigrateAsync(db, "things", null, cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _h.Lock.Released.Should().Be(1, "the lock is given up whatever happens");
        (await _h.TablesAsync()).Should().BeEmpty();
    }

    // --- a migration merged out of order ---

    [Fact]
    public async Task AMigrationOlderThanOneAlreadyApplied_IsApplied_WithoutRevertingTheNewerOne()
    {
        await using var db = _h.Context<LettersContext>();
        await db.Database.MigrateAsync(Ct);
        // The database as it would be had B been merged after C was released.
        await _h.Run("DROP TABLE b");
        await _h.Run(
            "DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20260101000002_CreateB'"
        );

        var state = await _h.Migrator().InspectAsync(db, Ct);
        state.Pending.Should().Equal("20260101000002_CreateB");
        state.HasOutOfOrderPending.Should().BeTrue();

        var result = await _h.Migrator().MigrateAsync(db, "letters", null, Ct);

        result.Applied.Should().Equal("20260101000002_CreateB");
        (await _h.TablesAsync()).Should().Contain(["a", "b", "c"]);
        (await _h.HistoryAsync())
            .Should()
            .Equal("20260101000001_CreateA", "20260101000002_CreateB", "20260101000003_CreateC");
    }

    // --- reading the state, and the log ---

    [Fact]
    public async Task TheStateOfADatabaseThatDoesNotExistYet_IsEverythingPending()
    {
        await using var db = _h.Context<ThingsContext>();

        var state = await _h.Migrator().InspectAsync(db, Ct);

        state.Applied.Should().BeEmpty();
        state.Pending.Should().Equal(CREATE, NOTE, DROP);
        state.Unknown.Should().BeEmpty();
        state.Current.Should().BeNull();
        state.HasOutOfOrderPending.Should().BeFalse();
    }

    [Fact]
    public async Task APluginReloadingWithNothingNewSaysNothingAtInformationLevel()
    {
        await using var db = _h.Context<ThingsContext>();
        await _h.Migrator().MigrateAsync(db, "things", null, Ct);
        _h.Log.Entries.Clear();

        await _h.Migrator()
            .MigrateAsync(db, "things", new MigrationRunOptions { QuietWhenCurrent = true }, Ct);

        Messages(LogLevel.Information).Should().BeEmpty();
        Messages(LogLevel.Debug).Should().ContainSingle().Which.Should().Contain("up to date");
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "a")]
    [InlineData(6, "a, b, c, d, e, f")]
    public void FewMigrationsAreListedByName(int count, string expected) =>
        DatabaseMigrator
            .Join(new[] { "a", "b", "c", "d", "e", "f" }.Take(count))
            .Should()
            .Be(expected);

    [Fact]
    public void ManyMigrationsAreListedByTheirFirstAndTheirLast() =>
        DatabaseMigrator
            .Join(Enumerable.Range(1, 47).Select(i => $"m{i:00}"))
            .Should()
            .Be("m01 ... m47");
}
