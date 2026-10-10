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
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Editing;

public sealed partial class CatalogEditService
{
    /// <summary>How many gone furniture or currency ids a refused rollback names.</summary>
    private const int MISSING_SHOWN_MAX = 5;

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
