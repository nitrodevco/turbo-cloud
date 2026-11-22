using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Migrations;
using Turbo.Import;
using Turbo.Import.Morningstar;
using Turbo.Inventory.Configuration;
using Turbo.Players.Configuration;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic;

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

    /// <summary>
    /// What an import is told about this server: the command's choices, and from the host's
    /// configuration how pets level, the limits what it copies is held to, and which furni logic
    /// exists and which one a definition runs, as the room decides it
    /// (<see cref="RoomObjectLogics"/>). Plugins are not loaded for an import, so only core's
    /// logic counts.
    /// </summary>
    internal static MorningstarImportOptions Options(
        ImportCommandOptions command,
        IServiceProvider services
    )
    {
        var pets = Config<PetConfig>(services);
        var rooms = Config<RoomConfig>(services);
        var wired = Config<WiredConfig>(services);
        var players = Config<PlayerConfig>(services);
        var gifts = Config<CatalogConfig>(services).GiftWrapping;
        var keys = RoomObjectLogics
            .In(typeof(RoomModule).Assembly)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new()
        {
            DryRun = command.DryRun,
            Catalog = command.Catalog,
            Chatlogs = command.Chatlogs,
            RankGroups = command.RankGroups,
            Pets = new PetRules
            {
                LevelExperience = pets.LevelExperienceThresholds,
                MaxLevel = pets.MaxLevel,
                MonsterplantMaxLevel = pets.MonsterplantMaxLevel,
                MonsterplantGrowthSeconds = pets.MonsterplantGrowthSeconds,
                MonsterplantWellBeingSeconds = pets.MonsterplantWellBeingSeconds,
                MonsterplantBabyWellBeingSeconds = pets.MonsterplantBabyWellBeingSeconds,
            },
            Limits = new ImportLimits
            {
                RoomNameMaxLength = rooms.RoomNameMaxLength,
                RoomDescriptionMaxLength = rooms.RoomDescriptionMaxLength,
                RoomPlayersMax = rooms.MaxPlayersLimit,
                RoomTagsMax = rooms.RoomTagsMax,
                RoomTagMaxLength = rooms.RoomTagMaxLength,
                MessengerMessageMaxLength = players.MessengerMaxMessageLength,
                WiredSelectedItemsMax = wired.SelectedItemsLimit,
                WiredActionDelayMaxPulses = wired.ActionDelayMaxPulses,
                ActiveBadgesMax = Config<InventoryConfig>(services).MaxActiveBadges,
                GiftBoxTypes = gifts.BoxTypes.ToHashSet(),
                GiftRibbonTypes = gifts.RibbonTypes.ToHashSet(),
            },
            LogicKeys = keys,
            LogicOf = definition =>
                RoomObjectLogics.Resolve(
                    definition.LogicName,
                    definition,
                    keys.Contains,
                    rooms.WaterAreaLogicByDefinition
                ),
        };
    }

    private static T Config<T>(IServiceProvider services)
        where T : class => services.GetRequiredService<IOptions<T>>().Value;

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
                    Options(options, services),
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
