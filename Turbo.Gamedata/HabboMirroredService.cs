using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata;

/// <summary>
/// The hotel's copy of one of Habbo's files, row by row - its texts, product data or figure data -
/// taken in by comparing each row three ways: Habbo's new one, Habbo's as last taken in
/// (<typeparamref name="TBase"/>) and the hotel's (<typeparamref name="TOurs"/>). What is the same
/// for each kind lives here: the preview, kept until its rows change, and the import, one
/// transaction and one change set. A kind supplies its plan, how a row takes Habbo's values and
/// the change that records it, and what to forget after a change (<see cref="Changed"/>).
/// </summary>
internal abstract class HabboMirroredService<TVersion, TOurs, TBase, TItem, TPreview>(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    GamedataWriteLock writes,
    TimeProvider time,
    ILogger logger
)
    where TVersion : TurboEntity, IHabboVersionEntity
    where TOurs : TurboEntity
    where TBase : TurboEntity
    where TItem : IHabboImportItem<TOurs, TBase>
    where TPreview : class
{
    private readonly GamedataPreviewCache<GamedataDataVersion, TPreview> _previews = new();

    protected IDbContextFactory<TurboDbContext> DbCtxFactory { get; } = dbCtxFactory;

    protected GamedataConfig Config { get; } = config.Value;

    protected GamedataWriteLock Writes { get; } = writes;

    /// <summary>What the file is called in the log: <c>texts</c>, <c>product data</c>.</summary>
    protected abstract string What { get; }

    /// <summary>What taking in the version would do; the newest when none is named. Null when there is no such version.</summary>
    public async Task<TPreview?> PreviewImportAsync(int? versionId, CancellationToken ct)
    {
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await dbCtx
            .Set<TVersion>()
            .Latest(Config.HabboDomain)
            .AsNoTracking()
            .Select(x => new { x.Id, x.ImportedAt })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        var data = await GamedataDataVersion
            .ReadAsync(
                dbCtx,
                latest?.Id,
                latest?.ImportedAt,
                dbCtx.Set<TOurs>().Select(x => x.Id),
                dbCtx.Set<TBase>().Select(x => x.Id),
                ct
            )
            .ConfigureAwait(false);

        return await _previews
            .GetAsync(data, versionId, () => WorkOutPreviewAsync(dbCtx, versionId, ct), ct)
            .ConfigureAwait(false);
    }

    private async Task<TPreview?> WorkOutPreviewAsync(
        TurboDbContext dbCtx,
        int? versionId,
        CancellationToken ct
    )
    {
        var version = await dbCtx
            .Set<TVersion>()
            .FindOrLatestAsync(versionId, Config.HabboDomain, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: false, ct).ConfigureAwait(false);
        var touched = plan.Where(x => x.Action is not null).ToList();

        return Preview(
            version,
            plan,
            touched.Take(Config.PreviewItemLimit),
            touched.Count > Config.PreviewItemLimit
        );
    }

    /// <summary>
    /// Takes the version in. Null when there is no such version, or when the hotel already had
    /// everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    ) => Writes.RunAsync(() => ImportLockedAsync(versionId, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> ImportLockedAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    )
    {
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await dbCtx
            .Set<TVersion>()
            .FindOrLatestAsync(versionId, Config.HabboDomain, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: true, ct).ConfigureAwait(false);
        var changes = new List<(GamedataChangeEntity Change, TurboEntity Row)>();

        // Big imports (a first one is every row Habbo has) are written without EF looking for
        // changes on each of tens of thousands of rows at every step.
        dbCtx.ChangeTracker.AutoDetectChangesEnabled = false;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        foreach (var item in plan)
        {
            if (item.Action == FurnitureImportAction.Add)
            {
                var (row, change) = AddOurs(item);

                dbCtx.Set<TOurs>().Add(row);
                changes.Add((change, row));
            }
            else if (item.Action == FurnitureImportAction.Update)
            {
                var row = item.Current!;
                var change = UpdateOurs(item, row);

                dbCtx.Entry(row).State = EntityState.Modified;
                changes.Add((change, row));
            }

            if (item.Base is { } known)
            {
                if (TakeHabbo(item, known, version.Id) is not { } change)
                    continue;

                changes.Add((change, known));
                dbCtx.Entry(known).State = EntityState.Modified;
            }
            else
            {
                var (row, change) = AddHabbo(item, version.Id);

                dbCtx.Set<TBase>().Add(row);
                changes.Add((change, row));
            }
        }

        version.ImportedAt = time.GetUtcNow().UtcDateTime;
        dbCtx.Entry(version).State = EntityState.Modified;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var added = Count(plan, FurnitureImportAction.Add);
        var updated = Count(plan, FurnitureImportAction.Update);
        var kept = Count(plan, FurnitureImportAction.Keep);
        GamedataChangeSetEntity? changeSet = null;

        if (changes.Count > 0)
        {
            // Rows made by this import have their ids only now that they are saved.
            foreach (var (change, row) in changes)
                change.RecordId = row.Id;

            changeSet = GamedataChangeSets.Create(
                GamedataChangeKind.Import,
                ImportSummary(version, added, updated, kept),
                player,
                [.. changes.Select(x => x.Change)]
            );
            dbCtx.GamedataChangeSets.Add(changeSet);

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Imported Habbo's {What} {VersionId}: {Added} added, {Updated} updated, {Kept} kept as the hotel has them",
            What,
            version.Id,
            added,
            updated,
            kept
        );

        Changed();

        return changeSet?.ToSnapshot(changeSet.Changes!.Count);
    }

    /// <summary>
    /// Removes the hotel's row <paramref name="which"/> finds, recorded as
    /// <paramref name="change"/> makes it; false when there is none.
    /// </summary>
    protected Task<bool> RemoveAsync(
        Expression<Func<TOurs, bool>> which,
        Func<TOurs, GamedataChangeEntity> change,
        string summary,
        PlayerId player,
        CancellationToken ct
    ) => Writes.RunAsync(() => RemoveLockedAsync(which, change, summary, player, ct), ct);

    private async Task<bool> RemoveLockedAsync(
        Expression<Func<TOurs, bool>> which,
        Func<TOurs, GamedataChangeEntity> change,
        string summary,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx.Set<TOurs>().FirstOrDefaultAsync(which, ct).ConfigureAwait(false);

        if (row is null)
            return false;

        var removed = change(row);

        removed.RecordId = row.Id;
        dbCtx.Set<TOurs>().Remove(row);
        dbCtx.GamedataChangeSets.Add(
            GamedataChangeSets.Create(GamedataChangeKind.Edit, summary, player, [removed])
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        Changed();

        return true;
    }

    /// <summary>Every row of Habbo's version, beside the hotel's and Habbo's last.</summary>
    protected abstract Task<List<TItem>> PlanAsync(
        TurboDbContext dbCtx,
        TVersion version,
        bool tracked,
        CancellationToken ct
    );

    /// <param name="shown">The items the import changes or keeps, as many as a preview lists.</param>
    /// <param name="truncated">Whether there are more than it lists.</param>
    protected abstract TPreview Preview(
        TVersion version,
        List<TItem> plan,
        IEnumerable<TItem> shown,
        bool truncated
    );

    /// <summary>The hotel's new row, with Habbo's values, and the change that records it.</summary>
    protected abstract (TOurs Row, GamedataChangeEntity Change) AddOurs(TItem item);

    /// <summary>Habbo's values written to the hotel's row where the plan takes them, and the change that records it.</summary>
    protected abstract GamedataChangeEntity UpdateOurs(TItem item, TOurs row);

    /// <summary>
    /// Habbo's row as last taken in made Habbo's new one, and the change that records it; null,
    /// and left alone, when it already is.
    /// </summary>
    protected abstract GamedataChangeEntity? TakeHabbo(TItem item, TBase known, int versionId);

    /// <summary>Habbo's row of an item never taken in before, and the change that records it.</summary>
    protected abstract (TBase Row, GamedataChangeEntity Change) AddHabbo(TItem item, int versionId);

    /// <summary>What the import's change set is called.</summary>
    protected abstract string ImportSummary(TVersion version, int added, int updated, int kept);

    /// <summary>After any change: what was built or kept from the rows is forgotten.</summary>
    protected abstract void Changed();

    protected static int Count(List<TItem> plan, FurnitureImportAction? action) =>
        plan.Count(x => x.Action == action);
}
