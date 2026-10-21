using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Editing;

public sealed partial class CatalogEditService
{
    /// <summary>How many gone furniture or currency ids a refused rollback names.</summary>
    private const int MISSING_SHOWN_MAX = 5;

    /// <summary>The name of the backup a reset takes of the catalog it empties.</summary>
    public const string RESET_BACKUP_NAME = "Before resetting the catalog";

    public async Task<IReadOnlyList<CatalogBackupSummary>> GetBackupsAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .CatalogBackups.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => new CatalogBackupSummary(
                x.Id,
                x.Name,
                x.PlayerId,
                x.CreatedAt,
                x.Automatic,
                x.Pages,
                x.Offers,
                x.Products,
                x.FeaturedItems
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<CatalogEditResult> BackupAsync(
        PlayerId editor,
        string? name,
        CancellationToken ct
    )
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length > CatalogBackupEntity.NAME_MAX_LENGTH)
            return CatalogEditResult.Refused(
                $"A backup's name is at most {CatalogBackupEntity.NAME_MAX_LENGTH} characters."
            );

        if (trimmed.Length == 0)
            trimmed =
                $"Backup of {Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = await CatalogEditJournal.ReadAllAsync(db, ct).ConfigureAwait(false);
        var backup = await SaveBackupAsync(db, editor, trimmed, automatic: false, rows, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} backed up the catalog as {BackupName} ({BackupId})",
            editor,
            backup.Name,
            backup.Id
        );

        return CatalogEditResult.Done(backup.Id);
    }

    public async Task<CatalogEditResult> RollbackAsync(
        PlayerId editor,
        int backupId,
        CancellationToken ct
    )
    {
        // Not while an undo or a redo is moving rows: both read the rows they then rewrite.
        await _historyGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);

            var backup = await db
                .CatalogBackups.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == backupId, ct)
                .ConfigureAwait(false);

            if (backup is null)
                return CatalogEditResult.Refused("That backup is gone.");

            Dictionary<CatalogRow, CatalogRowImage>? target;

            try
            {
                target = CatalogBackupFormat.Read(db.Model, backup.Data);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                logger.LogError(ex, "The catalog backup {BackupId} can't be read", backupId);
                target = null;
            }

            if (target is null)
                return CatalogEditResult.Refused("That backup can't be read.");

            var current = await CatalogEditJournal.ReadAllAsync(db, ct).ConfigureAwait(false);
            var step = new CatalogEditStep($"rolled back to the backup {backup.Name}", editor, Now);

            step.Add([
                .. current
                    .Keys.Union(target.Keys)
                    .Select(row => new CatalogRowChange(
                        row,
                        current.GetValueOrDefault(row),
                        target.GetValueOrDefault(row)
                    ))
                    .Where(x =>
                        x.Before is null
                        || x.After is null
                        || !CatalogEditJournal.Same(x.Before, x.After)
                    ),
            ]);

            if (!step.Changed)
                return CatalogEditResult.Refused("The catalog is already as that backup has it.");

            if (await CheckGoneAsync(db, step, ct).ConfigureAwait(false) is { } gone)
                return CatalogEditResult.Refused(gone);

            if (
                await CatalogEditJournal
                    .RestoreAsync(db, step, towardsBefore: false, ct)
                    .ConfigureAwait(false) is
                { } error
            )
                return CatalogEditResult.Refused(error);

            // What was there is kept too, so the rollback can be rolled back after a publish.
            var before = $"Before rolling back to {backup.Name}";
            var kept = await SaveBackupAsync(
                    db,
                    editor,
                    before[..Math.Min(before.Length, CatalogBackupEntity.NAME_MAX_LENGTH)],
                    automatic: true,
                    current,
                    ct
                )
                .ConfigureAwait(false);

            Interlocked.Increment(ref _unpublished);
            Push(step);

            logger.LogInformation(
                "Player {PlayerId} rolled the catalog back to {BackupName} ({BackupId}), changing {Rows} rows; what was there is backup {KeptId}",
                editor,
                backup.Name,
                backupId,
                step.Rows.Count(),
                kept.Id
            );

            return CatalogEditResult.Done(backupId);
        }
        finally
        {
            _historyGate.Release();
        }
    }

    public async Task<CatalogEditResult> ResetAsync(
        PlayerId editor,
        bool withLimited,
        CancellationToken ct
    )
    {
        // Not while an undo or a redo is moving rows: both read the rows they then rewrite.
        await _historyGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            CatalogEditStep? done = null;
            List<int> series = [];

            // One transaction: a reset refused, or one that fails, leaves the series, the
            // catalog and the backups as they were.
            var result = await dbCtxFactory
                .InTransactionAsync(
                    async db =>
                    {
                        var current = await CatalogEditJournal
                            .ReadAllAsync(db, ct)
                            .ConfigureAwait(false);
                        var step = new CatalogEditStep("reset the catalog", editor, Now);

                        // The root stays: every page is made under one, and the editor never
                        // takes it away.
                        step.Add([
                            .. current
                                .Where(x =>
                                    x.Key.Type != typeof(CatalogPageEntity)
                                    || x.Value.Values[nameof(CatalogPageEntity.ParentEntityId)]
                                        is not null
                                )
                                .Select(x => new CatalogRowChange(x.Key, x.Value, null)),
                        ]);

                        if (!step.Changed)
                            return UnitOfWork.Rollback(
                                CatalogEditResult.Refused("The catalog is already empty.")
                            );

                        series = await db
                            .LtdSeries.Select(x => x.Id)
                            .ToListAsync(ct)
                            .ConfigureAwait(false);

                        if (series.Count > 0 && !withLimited)
                            return UnitOfWork.Rollback(
                                CatalogEditResult.Refused(
                                    $"The catalog sells {series.Count} limited series: reset it with them taken away too, with their raffle entries and sales."
                                )
                            );

                        // Kept before anything is taken away.
                        var kept = await SaveBackupAsync(
                                db,
                                editor,
                                RESET_BACKUP_NAME,
                                automatic: true,
                                current,
                                ct
                            )
                            .ConfigureAwait(false);

                        await RemoveSeriesAsync(db, series, ct).ConfigureAwait(false);
                        await EmptyAsync(db, current, step, ct).ConfigureAwait(false);

                        done = step;

                        return UnitOfWork.Commit(CatalogEditResult.Done(kept.Id));
                    },
                    ct
                )
                .ConfigureAwait(false);

            if (done is null)
                return result;

            Interlocked.Increment(ref _unpublished);
            Push(done);

            logger.LogWarning(
                "Player {PlayerId} reset the catalog, taking away {Rows} rows and {Series} limited series; what was there is backup {KeptId}",
                editor,
                done.Rows.Count(),
                series.Count,
                result.Id
            );

            await ForgetRafflesAsync(series).ConfigureAwait(false);

            return result;
        }
        finally
        {
            _historyGate.Release();
        }
    }

    /// <summary>
    /// Takes away the rows a reset's step names, a thousand at a time by id. Not through the
    /// change tracker, as an undo puts rows back: a catalog of tens of thousands of offers would
    /// be loaded and deleted a row at a time, minutes against a database across a network, all
    /// while the panel waits. The step already holds each row as it was, for the undo, and the
    /// series that leaned on its products are gone before this runs.
    /// </summary>
    private static async Task EmptyAsync(
        TurboDbContext db,
        Dictionary<CatalogRow, CatalogRowImage> current,
        CatalogEditStep step,
        CancellationToken ct
    )
    {
        var rootId = current
            .Where(x =>
                x.Key.Type == typeof(CatalogPageEntity)
                && x.Value.Values[nameof(CatalogPageEntity.ParentEntityId)] is null
            )
            .Min(x => x.Key.Id);

        List<int> Ids<T>() =>
            [.. step.Rows.Where(x => x.Key.Type == typeof(T)).Select(x => x.Key.Id)];

        foreach (var chunk in Ids<CatalogFeaturedItemEntity>().Chunk(1000))
            await db
                .CatalogFeaturedItems.Where(x => chunk.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

        foreach (var chunk in Ids<CatalogProductEntity>().Chunk(1000))
            await db
                .CatalogProducts.Where(x => chunk.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

        foreach (var chunk in Ids<CatalogOfferEntity>().Chunk(1000))
            await db
                .CatalogOffers.Where(x => chunk.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

        // Pages hang off one another, so each is moved straight under the root first; then no
        // page being taken away is under another, and they can go in any order.
        var pages = Ids<CatalogPageEntity>();

        foreach (var chunk in pages.Chunk(1000))
            await db
                .CatalogPages.Where(x => chunk.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentEntityId, rootId), ct)
                .ConfigureAwait(false);

        foreach (var chunk in pages.Chunk(1000))
            await db
                .CatalogPages.Where(x => chunk.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
    }

    public async Task<CatalogEditResult> DeleteBackupAsync(
        PlayerId editor,
        int backupId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var deleted = await db
            .CatalogBackups.Where(x => x.Id == backupId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (deleted == 0)
            return CatalogEditResult.Refused("That backup is gone.");

        logger.LogInformation(
            "Player {PlayerId} deleted the catalog backup {BackupId}",
            editor,
            backupId
        );

        return CatalogEditResult.Done(backupId);
    }

    private static async Task<CatalogBackupEntity> SaveBackupAsync(
        TurboDbContext db,
        PlayerId editor,
        string name,
        bool automatic,
        Dictionary<CatalogRow, CatalogRowImage> rows,
        CancellationToken ct
    )
    {
        var backup = new CatalogBackupEntity
        {
            Name = name,
            PlayerId = editor.Value,
            Automatic = automatic,
            Pages = Count<CatalogPageEntity>(),
            Offers = Count<CatalogOfferEntity>(),
            Products = Count<CatalogProductEntity>(),
            FeaturedItems = Count<CatalogFeaturedItemEntity>(),
            Data = CatalogBackupFormat.Write(db.Model, rows),
        };

        db.CatalogBackups.Add(backup);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return backup;

        int Count<T>() => rows.Keys.Count(x => x.Type == typeof(T));
    }

    /// <summary>
    /// Why a rollback can't write its rows: a product names furniture, or an offer a currency,
    /// that the hotel no longer has. Null when everything it names is there.
    /// </summary>
    private static async Task<string?> CheckGoneAsync(
        TurboDbContext db,
        CatalogEditStep step,
        CancellationToken ct
    )
    {
        var written = step.Rows.Where(x => x.Value.After is not null).ToList();
        var definitions = IdsNamed(
            written,
            typeof(CatalogProductEntity),
            nameof(CatalogProductEntity.FurnitureDefinitionEntityId)
        );
        var currencies = IdsNamed(
            written,
            typeof(CatalogOfferEntity),
            nameof(CatalogOfferEntity.CurrencyTypeId)
        );
        var knownDefinitions = new HashSet<int>();

        // A thousand at a time, so no statement carries a whole generated catalog's ids.
        foreach (var chunk in definitions.Chunk(1000))
        {
            var part = chunk.ToList();

            knownDefinitions.UnionWith(
                await db
                    .FurnitureDefinitions.Where(x => part.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            );
        }

        var knownCurrencies = await db
            .CurrencyTypes.Where(x => currencies.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var goneDefinitions = definitions.Except(knownDefinitions).Order().ToList();
        var goneCurrencies = currencies.Except(knownCurrencies).Order().ToList();

        if (goneDefinitions.Count > 0)
            return $"The backup sells furniture the hotel no longer has ({Ids(goneDefinitions)}).";

        if (goneCurrencies.Count > 0)
            return $"The backup prices offers in a currency the hotel no longer has ({Ids(goneCurrencies)}).";

        return null;

        static HashSet<int> IdsNamed(
            List<KeyValuePair<CatalogRow, (CatalogRowImage? Before, CatalogRowImage? After)>> rows,
            Type type,
            string column
        ) =>
            [
                .. rows.Where(x => x.Key.Type == type)
                    .Select(x => x.Value.After!.Values.GetValueOrDefault(column))
                    .OfType<int>(),
            ];

        static string Ids(List<int> ids) =>
            ids.Count <= MISSING_SHOWN_MAX
                ? $"ids {string.Join(", ", ids)}"
                : $"ids {string.Join(", ", ids.Take(MISSING_SHOWN_MAX))} and {ids.Count - MISSING_SHOWN_MAX} more";
    }
}
