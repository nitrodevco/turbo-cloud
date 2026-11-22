using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Import.Morningstar.Steps;
using Turbo.Import.Source;

namespace Turbo.Import.Morningstar;

/// <summary>
/// Copies an Arcturus Morningstar hotel (3.x, and the 4.0 development schema) into Turbo's
/// database: the hotel's setup Turbo needs to place what players own (furniture definitions,
/// room models, navigator categories, currencies), then the players and everything of theirs.
/// <para>
/// Morningstar's ids are kept, so the target must hold no players, rooms or furniture yet: a
/// fresh database after <c>Turbo.Main migrate</c>, or one emptied with
/// <c>scripts/sql/fresh-start.sql</c>. The setup it adds is merged with what is there, so a failed
/// import is run again after <c>fresh-start.sql</c>.
/// </para>
/// </summary>
public sealed class MorningstarImporter(
    TurboDbContext db,
    MorningstarImportOptions options,
    ILogger<MorningstarImporter> log,
    TimeProvider time
)
{
    /// <summary>Imports from a MySQL or MariaDB database, by connection string.</summary>
    public async Task<ImportReport> RunAsync(string sourceConnectionString, CancellationToken ct)
    {
        SourceDatabase source;

        try
        {
            source = await SourceDatabase
                .OpenMySqlAsync(sourceConnectionString, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or ArgumentException)
        {
            throw new ImportException(
                $"Cannot read the Morningstar database: {ex.Message} Check --source.",
                ex
            );
        }

        await using (source.ConfigureAwait(false))
            return await RunAsync(source, ct).ConfigureAwait(false);
    }

    /// <summary>Imports from a database that is already open (a test's).</summary>
    internal async Task<ImportReport> RunAsync(SourceDatabase source, CancellationToken ct)
    {
        var report = new ImportReport();
        var ctx = new ImportContext(source, db, options, report, log, time.GetUtcNow().UtcDateTime);

        if (!source.HasTable("users") || !source.HasTable("items_base"))
            throw new ImportException(
                $"'{source.Name}' is not a Morningstar database: it has no users or items_base table."
            );

        foreach (var step in Steps())
        {
            if (!step.Applies(ctx))
                continue;

            var watch = Stopwatch.StartNew();

            log.LogInformation("Importing {Step}...", step.Name);

            await step.RunAsync(ctx, ct).ConfigureAwait(false);

            log.LogInformation("{Step} done in {Elapsed:mm\\:ss}", step.Name, watch.Elapsed);
        }

        return report;
    }

    /// <summary>In order: each reads what the ones before it copied.</summary>
    private static IEnumerable<IImportStep> Steps() =>
        [
            new PreflightStep(),
            new CurrencyTypesStep(),
            new RoomModelsStep(),
            new NavigatorCategoriesStep(),
            new FurnitureDefinitionsStep(),
            new PlayersStep(),
            new PlayerCollectionsStep(),
            new MessengerStep(),
            new RoomsStep(),
            new PlayerSettingsStep(),
            new GuildsStep(),
            new SongsStep(),
            new FurnitureStep(),
            new PetsStep(),
            new BotsStep(),
            new SanctionsStep(),
            new CatalogStep(),
            new ChatlogsStep(),
        ];
}
