using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Migrations;

namespace Turbo.Main.Startup;

/// <summary>
/// The emulator's own tables, before anything else touches them: migrated at startup the way
/// <c>Turbo:Database:Migrate</c> says, and by hand with <c>Turbo.Main migrate</c> for a hotel that
/// runs it as a deploy step. Plugins' tables are migrated as each plugin loads, by the same
/// <see cref="DatabaseMigrator"/>. See <c>docs/database.md</c>.
/// </summary>
internal static class DatabaseStartup
{
    public const int EXIT_OK = 0;
    public const int EXIT_FAILED = 1;

    /// <summary>With <c>--status</c>: the database is behind and nothing is wrong otherwise.</summary>
    public const int EXIT_PENDING = 2;

    public const int EXIT_USAGE = 64;

    /// <summary>Before the host starts: the emulator's own tables, as the configuration says.</summary>
    public static Task<MigrationResult> MigrateAsync(
        IServiceProvider services,
        CancellationToken ct
    ) => RunAsync(services, null, ct);

    /// <summary><c>Turbo.Main migrate</c>. Returns the process exit code.</summary>
    public static async Task<int> RunCommandAsync(
        IServiceProvider services,
        MigrateCommandOptions options,
        ILogger log,
        CancellationToken ct
    )
    {
        try
        {
            if (options.Status)
                return await StatusAsync(services, log, ct).ConfigureAwait(false);

            if (options.ScriptPath is { } path)
                return await ScriptAsync(services, path, log, ct).ConfigureAwait(false);

            await RunAsync(
                    services,
                    new MigrationRunOptions
                    {
                        Mode = MigrationMode.Auto,
                        AllowDestructive = options.AllowDestructive ? true : null,
                    },
                    ct
                )
                .ConfigureAwait(false);

            log.LogInformation(
                "Plugins' tables are migrated when each plugin loads as the server starts."
            );

            return EXIT_OK;
        }
        catch (MigrationException ex)
        {
            log.LogCritical("{Message}", ex.Message);

            return EXIT_FAILED;
        }
    }

    public static void PrintUsage(ILogger log, string? error)
    {
        if (error is not null)
            log.LogError("{Error}", error);

        // Not through the logger: it puts a message on one line, and this is read, not parsed.
        System.Console.WriteLine(MigrateCommandLine.USAGE);
    }

    private static async Task<MigrationResult> RunAsync(
        IServiceProvider services,
        MigrationRunOptions? run,
        CancellationToken ct
    )
    {
        var migrator = services.GetRequiredService<DatabaseMigrator>();

        // Nothing to connect to: with migrations off the schema is not looked at.
        if ((run?.Mode ?? Config(services).Migrate) == MigrationMode.Off)
            return migrator.Skip(DatabaseMigrator.CORE_LABEL);

        return await WithCoreAsync(
                services,
                db => migrator.MigrateAsync(db, DatabaseMigrator.CORE_LABEL, run, ct),
                ct
            )
            .ConfigureAwait(false);
    }

    private static async Task<int> StatusAsync(
        IServiceProvider services,
        ILogger log,
        CancellationToken ct
    )
    {
        var migrator = services.GetRequiredService<DatabaseMigrator>();
        var state = await WithCoreAsync(services, db => migrator.InspectAsync(db, ct), ct)
            .ConfigureAwait(false);

        if (state.Unknown.Count > 0)
        {
            log.LogCritical(
                "Database core has migrations this version of Turbo does not know: {Unknown}. It was last used by a newer version.",
                string.Join(", ", state.Unknown)
            );

            return EXIT_FAILED;
        }

        if (state.Pending.Count == 0)
        {
            log.LogInformation("Database core: up to date at {Current}", state.Current);

            return EXIT_OK;
        }

        log.LogWarning(
            "Database core: {Count} migration(s) behind: {Pending}",
            state.Pending.Count,
            DatabaseMigrator.Join(state.Pending)
        );

        return EXIT_PENDING;
    }

    private static async Task<int> ScriptAsync(
        IServiceProvider services,
        string path,
        ILogger log,
        CancellationToken ct
    )
    {
        var sql = await WithCoreAsync(
                services,
                db => Task.FromResult(DatabaseMigrator.GenerateScript(db)),
                ct
            )
            .ConfigureAwait(false);

        await File.WriteAllTextAsync(path, sql, ct).ConfigureAwait(false);

        log.LogInformation(
            "Wrote the SQL for every migration to {Path}. It is safe to run on a database at any earlier migration, and twice.",
            Path.GetFullPath(path)
        );

        return EXIT_OK;
    }

    private static DatabaseConfig Config(IServiceProvider services) =>
        services.GetRequiredService<IOptions<DatabaseConfig>>().Value;

    /// <summary>
    /// A context for the emulator's tables, with the failures an operator can fix said as such: no
    /// connection string, or a server that cannot be reached.
    /// </summary>
    private static async Task<T> WithCoreAsync<T>(
        IServiceProvider services,
        Func<TurboDbContext, Task<T>> body,
        CancellationToken ct
    )
    {
        var config = Config(services);

        if (string.IsNullOrWhiteSpace(config.ConnectionString))
            throw new MigrationException(
                "Turbo:Database:ConnectionString is not set. Give the server the database to use "
                    + "(appsettings, or TURBO_DB_* in the Ploi environment)."
            );

        try
        {
            var factory = services.GetRequiredService<IDbContextFactory<TurboDbContext>>();
            var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            await using (db.ConfigureAwait(false))
                return await body(db).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is MySqlException or FormatException)
        {
            throw new MigrationException(
                $"Cannot use the database: {ex.Message} Check Turbo:Database:ConnectionString "
                    + "(and Turbo:Database:ServerVersion, if set), and that the database server is running.",
                ex
            );
        }
    }
}
