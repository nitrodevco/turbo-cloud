using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;

namespace Turbo.Database.Migrations;

/// <summary>
/// Brings a database up to the migrations this build ships, for the emulator's own context at
/// startup and for each plugin's as it loads. See <c>docs/database.md</c>.
/// <list type="bullet">
/// <item>What it will not do: run against a database that has migrations this build does not know
/// (it was used by a newer version, and a migration cannot run backwards), or apply a migration
/// that drops a table or a column to a database that has data unless that was allowed.</item>
/// <item>What it does: one lock per database, so two migrators take turns; each pending migration
/// applied and timed on its own, so a long one shows progress and a failed one is named; the
/// state read again once the lock is held, so a migration another process applied meanwhile is
/// not applied twice.</item>
/// </list>
/// Every refusal is a <see cref="MigrationException"/> whose message is the whole report.
/// </summary>
public sealed class DatabaseMigrator(
    IOptions<DatabaseConfig> options,
    IMigrationLock migrationLock,
    ILogger<DatabaseMigrator> logger
)
{
    /// <summary>What the emulator's own tables are called in the log, as opposed to a plugin's.</summary>
    public const string CORE_LABEL = "core";

    /// <summary>Where <paramref name="db"/> stands against the migrations this build ships.</summary>
    public async Task<MigrationState> InspectAsync(DbContext db, CancellationToken ct)
    {
        var known = db.GetService<IMigrationsAssembly>()
            .Migrations.Keys.Order(StringComparer.Ordinal)
            .ToList();

        var exists = await db.GetService<IRelationalDatabaseCreator>()
            .ExistsAsync(ct)
            .ConfigureAwait(false);

        var applied = exists
            ? (await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var appliedSet = applied.ToHashSet(StringComparer.Ordinal);

        return new MigrationState(
            applied,
            [.. known.Where(id => !appliedSet.Contains(id))],
            [.. applied.Where(id => !knownSet.Contains(id))],
            exists
        );
    }

    /// <summary>
    /// The SQL that takes any database, empty or at any earlier migration, to the latest, safe to
    /// run twice. Needs no connection beyond what <paramref name="db"/>'s options already say.
    /// </summary>
    public static string GenerateScript(DbContext db) =>
        db.GetService<IMigrator>()
            .GenerateScript(
                fromMigration: null,
                toMigration: null,
                MigrationsSqlGenerationOptions.Idempotent
            );

    /// <summary>
    /// What migrations being off comes to: the schema is not looked at, and the log says so.
    /// Needs no database, so a server that is not allowed to touch its schema need not reach it.
    /// </summary>
    public MigrationResult Skip(string label)
    {
        logger.LogInformation(
            "Database {Label}: migrations are off (Turbo:Database:Migrate); the schema is not checked",
            label
        );

        return new MigrationResult(MigrationOutcome.Skipped, [], null, TimeSpan.Zero);
    }

    /// <summary>
    /// Does what the mode says to <paramref name="db"/>, called <paramref name="label"/> in the log.
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        DbContext db,
        string label,
        MigrationRunOptions? run = null,
        CancellationToken ct = default
    )
    {
        var config = options.Value;
        var mode = run?.Mode ?? config.Migrate;

        if (mode == MigrationMode.Off)
            return Skip(label);

        var clock = Stopwatch.StartNew();
        var state = await InspectAsync(db, ct).ConfigureAwait(false);

        RefuseUnknown(label, state);

        if (state.Pending.Count == 0)
            return UpToDate(label, state, run, clock);

        if (mode == MigrationMode.Check)
            throw new MigrationException(
                $"Database {label} is {state.Pending.Count} migration(s) behind: {Join(state.Pending)}. "
                    + (
                        label == CORE_LABEL
                            ? "Run 'Turbo.Main migrate' (or your deploy script) first, "
                                + "or set Turbo:Database:Migrate to Auto to let the server do it."
                            : "A plugin's tables are migrated when the plugin loads: set "
                                + "Turbo:Database:Migrate to Auto and start the server ('Turbo.Main migrate' "
                                + "does only the emulator's own tables)."
                    )
            );

        var lockWait = TimeSpan.FromSeconds(Math.Max(1, config.MigrationLockSeconds));
        var connectionString =
            db.Database.GetConnectionString()
            ?? throw new MigrationException(
                $"Database {label} has no connection string (Turbo:Database:ConnectionString)."
            );

        var applied = new List<string>();

        var held = await migrationLock
            .AcquireAsync(connectionString, lockWait, ct)
            .ConfigureAwait(false);

        await using (held.ConfigureAwait(false))
        {
            // Whatever was true before the wait may not be now: another server may have done it.
            state = await InspectAsync(db, ct).ConfigureAwait(false);

            RefuseUnknown(label, state);

            if (state.Pending.Count == 0)
                return UpToDate(label, state, run, clock);

            RefuseDestructive(
                db,
                label,
                state,
                run?.AllowDestructive ?? config.AllowDestructiveMigrations
            );

            db.Database.SetCommandTimeout(
                TimeSpan.FromMinutes(Math.Max(1, config.MigrationCommandTimeoutMinutes))
            );

            Announce(label, state);

            await ApplyAsync(db, label, state, applied, ct).ConfigureAwait(false);
        }

        var after = await InspectAsync(db, ct).ConfigureAwait(false);

        if (after.Pending.Count > 0)
            throw new MigrationException(
                $"Database {label} still has pending migrations after they were applied: {Join(after.Pending)}. "
                    + "Something else is changing the schema."
            );

        logger.LogInformation(
            "Database {Label}: up to date at {Current}, {Count} migration(s) applied in {Elapsed}",
            label,
            after.Current,
            applied.Count,
            Format(clock.Elapsed)
        );

        return new MigrationResult(MigrationOutcome.Applied, applied, after.Current, clock.Elapsed);
    }

    private MigrationResult UpToDate(
        string label,
        MigrationState state,
        MigrationRunOptions? run,
        Stopwatch clock
    )
    {
        logger.Log(
            run?.QuietWhenCurrent == true ? LogLevel.Debug : LogLevel.Information,
            "Database {Label}: up to date at {Current}",
            label,
            state.Current ?? "(no migrations)"
        );

        return new MigrationResult(MigrationOutcome.UpToDate, [], state.Current, clock.Elapsed);
    }

    private static void RefuseUnknown(string label, MigrationState state)
    {
        if (state.Unknown.Count == 0)
            return;

        throw new MigrationException(
            $"Database {label} has migrations this version of Turbo does not know: {Join(state.Unknown)}. "
                + "It was last used by a newer version, and a migration cannot be undone by an older one. "
                + "Run the newer version, or restore a backup taken before the upgrade."
        );
    }

    /// <summary>
    /// A migration that drops a table or a column is refused on a database that already has data
    /// unless it was allowed: an empty one has nothing to lose, and the first run creates it.
    /// </summary>
    private void RefuseDestructive(DbContext db, string label, MigrationState state, bool allowed)
    {
        if (state.Applied.Count == 0)
            return;

        var assembly = db.GetService<IMigrationsAssembly>();
        var provider = db.Database.ProviderName!;
        var found = new List<string>();

        foreach (var id in state.Pending)
        {
            foreach (
                var operation in assembly
                    .CreateMigration(assembly.Migrations[id], provider)
                    .UpOperations
            )
            {
                var what = operation switch
                {
                    DropTableOperation drop => $"drops table {drop.Name}",
                    DropColumnOperation drop => $"drops column {drop.Table}.{drop.Name}",
                    _ => null,
                };

                if (what is not null)
                    found.Add($"{id} {what}");
            }
        }

        if (found.Count == 0)
            return;

        if (!allowed)
            throw new MigrationException(
                $"Database {label} has data, and pending migrations would delete some of it: {string.Join("; ", found)}. "
                    + "Back up the database, then set Turbo:Database:AllowDestructiveMigrations to true "
                    + "(or run 'Turbo.Main migrate --allow-destructive') for this one start."
            );

        logger.LogWarning(
            "Database {Label}: destructive migrations are allowed and will run: {Found}",
            label,
            string.Join("; ", found)
        );
    }

    private void Announce(string label, MigrationState state)
    {
        if (state.Applied.Count == 0)
        {
            logger.LogInformation(
                "Database {Label}: {Created}, {Count} migration(s)",
                label,
                state.DatabaseExists ? "creating the tables" : "creating the database",
                state.Pending.Count
            );

            return;
        }

        logger.LogWarning(
            "Database {Label}: applying {Count} pending migration(s) to a database that has data: {Pending}. "
                + "A backup is the only way back.",
            label,
            state.Pending.Count,
            Join(state.Pending)
        );
    }

    private async Task ApplyAsync(
        DbContext db,
        string label,
        MigrationState state,
        List<string> applied,
        CancellationToken ct
    )
    {
        var migrator = db.GetService<IMigrator>();

        // Migrating "to" a migration older than one already applied would roll the newer ones
        // back, so a migration merged out of order is applied by one pass to the latest instead.
        List<string> steps = state.HasOutOfOrderPending ? [string.Empty] : [.. state.Pending];

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var name = step.Length == 0 ? Join(state.Pending) : step;
            var clock = Stopwatch.StartNew();

            logger.LogInformation(
                "Database {Label}: applying {Migration} ({Number}/{Total})",
                label,
                name,
                i + 1,
                steps.Count
            );

            try
            {
                await migrator
                    .MigrateAsync(step.Length == 0 ? null : step, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MigrationException(
                    $"Database {label}: migration {name} failed: {ex.Message} "
                        + $"{applied.Count} earlier migration(s) were applied. MySQL cannot roll back a "
                        + "schema change, so the database is left as the failure found it: fix the cause "
                        + "(or restore a backup) and start again. Applying resumes at this migration.",
                    ex
                );
            }

            if (step.Length == 0)
                applied.AddRange(state.Pending);
            else
                applied.Add(step);

            logger.LogInformation(
                "Database {Label}: applied {Migration} in {Elapsed}",
                label,
                name,
                Format(clock.Elapsed)
            );
        }
    }

    /// <summary>
    /// The migrations by name when there are few, and by the first and the last when a whole database
    /// is being created: the names in between say nothing to whoever reads this. The count is
    /// said next to it, where it matters.
    /// </summary>
    public static string Join(IEnumerable<string> ids)
    {
        var list = ids as IReadOnlyList<string> ?? [.. ids];

        return list.Count <= 6 ? string.Join(", ", list) : $"{list[0]} ... {list[^1]}";
    }

    private static string Format(TimeSpan elapsed) =>
        elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms"
            : $"{elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";
}
