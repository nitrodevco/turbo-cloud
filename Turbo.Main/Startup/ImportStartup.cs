using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Database.Context;
using Turbo.Database.Migrations;
using Turbo.Import;
using Turbo.Import.Morningstar;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;

namespace Turbo.Main.Startup;

/// <summary>
/// <c>Turbo.Main import morningstar</c>: copies another emulator's hotel into this server's
/// database, then exits. The emulator's tables are migrated first, as the server would at start.
/// See <c>docs/importing.md</c>.
/// </summary>
internal static class ImportStartup
{
    public static void PrintUsage(ILogger log, string? error)
    {
        if (error is not null)
            log.LogError("{Error}", error);

        // Not through the logger: it puts a message on one line, and this is read, not parsed.
        System.Console.WriteLine(ImportCommandLine.USAGE);
    }

    private static PetRules PetRules(PetConfig pets) =>
        new()
        {
            LevelExperience = pets.LevelExperienceThresholds,
            MaxLevel = pets.MaxLevel,
            MonsterplantMaxLevel = pets.MonsterplantMaxLevel,
            MonsterplantGrowthSeconds = pets.MonsterplantGrowthSeconds,
            MonsterplantWellBeingSeconds = pets.MonsterplantWellBeingSeconds,
            MonsterplantBabyWellBeingSeconds = pets.MonsterplantBabyWellBeingSeconds,
        };

    /// <summary>Returns the process exit code.</summary>
    public static async Task<int> RunCommandAsync(
        IServiceProvider services,
        ImportCommandOptions options,
        ILogger log,
        TimeProvider time,
        CancellationToken ct
    )
    {
        try
        {
            await DatabaseStartup.MigrateAsync(services, ct).ConfigureAwait(false);

            var factory = services.GetRequiredService<IDbContextFactory<TurboDbContext>>();
            var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            await using (db.ConfigureAwait(false))
            {
                var importer = new MorningstarImporter(
                    db,
                    new MorningstarImportOptions
                    {
                        DryRun = options.DryRun,
                        Catalog = options.Catalog,
                        Chatlogs = options.Chatlogs,
                        RankGroups = options.RankGroups,
                        Pets = PetRules(services.GetRequiredService<IOptions<PetConfig>>().Value),
                        LogicKeys = MorningstarImporter.LogicKeys(typeof(RoomModule).Assembly),
                    },
                    services.GetRequiredService<ILogger<MorningstarImporter>>(),
                    time
                );

                var report = await importer.RunAsync(options.Source!, ct).ConfigureAwait(false);

                // Read, not parsed: one block rather than a log line per table.
                System.Console.WriteLine(report.ToString());

                log.LogInformation(
                    options.DryRun
                        ? "Dry run finished: nothing was written."
                        : "Import finished. Start the server as usual."
                );
            }

            return DatabaseStartup.EXIT_OK;
        }
        catch (MigrationException ex)
        {
            log.LogCritical("{Message}", ex.Message);

            return DatabaseStartup.EXIT_FAILED;
        }
        catch (ImportException ex)
        {
            log.LogCritical("{Message}", ex.Message);

            return DatabaseStartup.EXIT_FAILED;
        }
        catch (MySqlException ex)
        {
            log.LogCritical(ex, "The import stopped on a database error: {Message}", ex.Message);

            return DatabaseStartup.EXIT_FAILED;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogCritical(ex, "The import stopped unexpectedly");

            return DatabaseStartup.EXIT_FAILED;
        }
    }
}
