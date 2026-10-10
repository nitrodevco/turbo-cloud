using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Editing;

/// <summary>A catalog row by its table and id.</summary>
internal readonly record struct CatalogRow(Type Type, int Id);

/// <summary>A row's columns by property name, as it was or as it became; null is no row.</summary>
internal sealed class CatalogRowImage(Dictionary<string, object?> values)
{
    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    public int Id => (int)Values[nameof(TurboEntity.Id)]!;
}

/// <summary>What one save did to a row: how it was before and after; a missing side is no row.</summary>
internal sealed record CatalogRowChange(
    CatalogRow Row,
    CatalogRowImage? Before,
    CatalogRowImage? After
);

/// <summary>
/// One step of the editor's history: the rows it changed, each with how it was before the step
/// and how the step left it, however many edits it took. Undone, every row goes back to its
/// before; done again, to its after.
/// </summary>
internal sealed class CatalogEditStep(string label, PlayerId editor, DateTime atUtc)
{
    private readonly Dictionary<
        CatalogRow,
        (CatalogRowImage? Before, CatalogRowImage? After)
    > _rows = [];

    public string Label { get; } = label;

    public PlayerId Editor { get; } = editor;

    public DateTime AtUtc { get; } = atUtc;

    public int Edits { get; private set; }

    public IEnumerable<
        KeyValuePair<CatalogRow, (CatalogRowImage? Before, CatalogRowImage? After)>
    > Rows => _rows.Where(x => x.Value.Before is not null || x.Value.After is not null);

    public bool Changed => Rows.Any();

    /// <summary>An edit's changes: a row seen before keeps its first before and takes the latest after.</summary>
    public void Add(IReadOnlyList<CatalogRowChange> changes)
    {
        Edits++;

        foreach (var change in changes)
        {
            _rows[change.Row] = _rows.TryGetValue(change.Row, out var seen)
                ? (seen.Before, change.After)
                : (change.Before, change.After);
        }
    }

    public CatalogHistoryEntry Entry => new(Label, Editor, AtUtc, Edits);
}

/// <summary>
/// The catalog editor's journal: each save of pages, offers, their products and the featured
/// items is read off the change tracker as it is made, as row images before and after, so an
/// edit can be undone and done again exactly - ids and order included - and the edits since a
/// publish can all be thrown away. Limited series are not in it: the raffle sells from them at
/// once, so they are never a draft.
/// </summary>
internal static class CatalogEditJournal
{
    /// <summary>The journaled tables, each before the tables whose rows name its rows.</summary>
    public static readonly IReadOnlyList<Type> Tables =
    [
        typeof(CatalogPageEntity),
        typeof(CatalogOfferEntity),
        typeof(CatalogProductEntity),
        typeof(CatalogFeaturedItemEntity),
    ];

    private static readonly HashSet<Type> Journaled = [.. Tables];

    /// <summary>Set by the database when a row is made, and never compared.</summary>
    private const string CREATED_AT = nameof(TurboEntity.CreatedAt);

    /// <summary>
    /// Saves the context's changes and returns what they did to the journaled rows. The products
    /// of a deleted offer go with it in the database, so they are read first, to be put back.
    /// </summary>
    public static async Task<IReadOnlyList<CatalogRowChange>> SaveAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        db.ChangeTracker.DetectChanges();

        var entries = db
            .ChangeTracker.Entries()
            .Where(x =>
                Journaled.Contains(x.Metadata.ClrType)
                && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            )
            .Select(x =>
                (
                    Entry: x,
                    State: x.State,
                    Before: x.State == EntityState.Added ? null : Image(x.OriginalValues)
                )
            )
            .ToList();
        var deletedOffers = entries
            .Where(x => x.State == EntityState.Deleted && x.Entry.Entity is CatalogOfferEntity)
            .Select(x => x.Before!.Id)
            .ToList();
        var tracked = entries
            .Where(x => x.Entry.Entity is CatalogProductEntity)
            .Select(x => ((CatalogProductEntity)x.Entry.Entity).Id)
            .ToHashSet();
        var cascaded =
            deletedOffers.Count == 0
                ? []
                : await db
                    .CatalogProducts.AsNoTracking()
                    .Where(x => deletedOffers.Contains(x.CatalogOfferEntityId))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var changes = new List<CatalogRowChange>(entries.Count + cascaded.Count);

        foreach (var (entry, state, before) in entries)
        {
            var after = state == EntityState.Deleted ? null : Image(entry.CurrentValues);
            var id = (after ?? before)!.Id;

            changes.Add(new(new CatalogRow(entry.Metadata.ClrType, id), before, after));
        }

        var productType = db.Model.FindEntityType(typeof(CatalogProductEntity))!;

        foreach (var product in cascaded.Where(x => !tracked.Contains(x.Id)))
        {
            changes.Add(
                new(
                    new CatalogRow(typeof(CatalogProductEntity), product.Id),
                    Image(productType, product),
                    null
                )
            );
        }

        return changes;
    }

    /// <summary>Every journaled row as it is saved now.</summary>
    public static async Task<Dictionary<CatalogRow, CatalogRowImage>> ReadAllAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var rows = new Dictionary<CatalogRow, CatalogRowImage>();

        Add(await db.CatalogPages.AsNoTracking().ToListAsync(ct).ConfigureAwait(false));
        Add(await db.CatalogOffers.AsNoTracking().ToListAsync(ct).ConfigureAwait(false));
        Add(await db.CatalogProducts.AsNoTracking().ToListAsync(ct).ConfigureAwait(false));
        Add(await db.CatalogFeaturedItems.AsNoTracking().ToListAsync(ct).ConfigureAwait(false));

        return rows;

        void Add<T>(List<T> entities)
            where T : TurboEntity
        {
            var entityType = db.Model.FindEntityType(typeof(T))!;

            foreach (var entity in entities)
                rows[new CatalogRow(typeof(T), entity.Id)] = Image(entityType, entity);
        }
    }

    /// <summary>Whether a row is as an image says, but for when it was made.</summary>
    public static bool Same(CatalogRowImage left, CatalogRowImage right) =>
        left
            .Values.Where(x => x.Key != CREATED_AT)
            .All(x => ValueEquals(x.Value, right.Values.GetValueOrDefault(x.Key)));

    /// <summary>
    /// Puts every row of a step back as <paramref name="towardsBefore"/> says - to how it was
    /// before the step, or to how the step left it - in one save. Refused, with why, when a row
    /// is not as the step left it (or found it), or when a row it would take away has something
    /// leaning on it.
    /// </summary>
    public static async Task<string?> RestoreAsync(
        TurboDbContext db,
        CatalogEditStep step,
        bool towardsBefore,
        CancellationToken ct
    )
    {
        var rows = step
            .Rows.Select(x =>
                (
                    Row: x.Key,
                    Expected: towardsBefore ? x.Value.After : x.Value.Before,
                    Target: towardsBefore ? x.Value.Before : x.Value.After
                )
            )
            .ToList();
        var current = new Dictionary<CatalogRow, object>();

        foreach (var table in rows.GroupBy(x => x.Row.Type))
        {
            var ids = table.Select(x => x.Row.Id).ToList();

            foreach (var entity in await LoadAsync(db, table.Key, ids, ct).ConfigureAwait(false))
                current[new CatalogRow(table.Key, ((TurboEntity)entity).Id)] = entity;
        }

        foreach (var (row, expected, _) in rows)
        {
            var found = current.GetValueOrDefault(row);
            var same = found is null
                ? expected is null
                : expected is not null && Same(Image(db.Entry(found).CurrentValues), expected);

            if (!same)
                return $"{Describe(row, expected ?? (found is null ? null : Image(db.Entry(found).CurrentValues)))} was changed since, so this can't be put back.";
        }

        var removing = rows.Where(x => x.Target is null && current.ContainsKey(x.Row)).ToList();

        // A row the step puts somewhere else is leaving whatever is taken away, not taken with it.
        var kept = rows.Where(x => x.Target is not null).Select(x => x.Row).ToList();

        if (
            await CheckRemovalAsync(db, [.. removing.Select(x => x.Row)], kept, ct)
                .ConfigureAwait(false) is
            { } refused
        )
            return refused;

        // Rows are put back and made first and taken away last, and a page taken away takes
        // only the offers still on it when it is saved: an offer the step moves back off it stays.
        db.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        db.ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;

        foreach (var (row, _, target) in rows)
        {
            var found = current.GetValueOrDefault(row);

            if (target is null)
                continue;

            if (found is not null)
            {
                var values = db.Entry(found).CurrentValues;

                foreach (var property in values.Properties)
                {
                    if (property.IsPrimaryKey() || property.Name == CREATED_AT)
                        continue;

                    values[property] = Copy(target.Values.GetValueOrDefault(property.Name));
                }

                continue;
            }

            var entityType = db.Model.FindEntityType(row.Type)!;
            var entity = Activator.CreateInstance(row.Type)!;

            foreach (var property in entityType.GetProperties())
                property.PropertyInfo?.SetValue(
                    entity,
                    Copy(target.Values.GetValueOrDefault(property.Name))
                );

            db.Add(entity);
        }

        foreach (var (row, _, _) in removing)
            db.Remove(current[row]);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    /// What taking rows away would take with it that is not the editor's to take: a limited
    /// series sold from a product, Builders Club furni placed from an offer, or a page's offers
    /// and pages that are staying.
    /// </summary>
    private static async Task<string?> CheckRemovalAsync(
        TurboDbContext db,
        List<CatalogRow> removing,
        List<CatalogRow> kept,
        CancellationToken ct
    )
    {
        var offers = IdsOf<CatalogOfferEntity>(removing);
        var pages = IdsOf<CatalogPageEntity>(removing);
        var products = IdsOf<CatalogProductEntity>(removing);
        var leavingOffers = IdsOf<CatalogOfferEntity>(kept);
        var leavingPages = IdsOf<CatalogPageEntity>(kept);

        foreach (var chunk in offers.Chunk(1000))
        {
            var part = chunk.ToList();

            products.UnionWith(
                await db
                    .CatalogProducts.Where(x => part.Contains(x.CatalogOfferEntityId))
                    .Select(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            );

            if (
                await db
                    .BuildersClubFurnitures.AnyAsync(x => part.Contains(x.CatalogOfferEntityId), ct)
                    .ConfigureAwait(false)
            )
                return "Builders Club furni was placed from an offer it would take away.";
        }

        foreach (var chunk in products.Chunk(1000))
        {
            var part = chunk.ToList();

            if (
                await db
                    .LtdSeries.AnyAsync(x => part.Contains(x.CatalogProductEntityId), ct)
                    .ConfigureAwait(false)
            )
                return "An offer it would take away sells a limited series now.";
        }

        if (pages.Count == 0)
            return null;

        foreach (var chunk in pages.Chunk(1000))
        {
            var part = chunk.ToList();
            var onThem = await db
                .CatalogOffers.Where(x => part.Contains(x.CatalogPageEntityId))
                .Select(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (!onThem.All(x => offers.Contains(x) || leavingOffers.Contains(x)))
                return "A page it would take away has offers on it now.";

            var under = await db
                .CatalogPages.Where(x =>
                    x.ParentEntityId != null && part.Contains(x.ParentEntityId.Value)
                )
                .Select(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (!under.All(x => pages.Contains(x) || leavingPages.Contains(x)))
                return "A page it would take away has pages under it now.";
        }

        return null;
    }

    private static HashSet<int> IdsOf<T>(List<CatalogRow> rows) =>
        [.. rows.Where(x => x.Type == typeof(T)).Select(x => x.Id)];

    private static async Task<List<object>> LoadAsync(
        TurboDbContext db,
        Type type,
        List<int> ids,
        CancellationToken ct
    )
    {
        var rows = new List<object>();

        // A thousand at a time, so no statement carries a whole generated catalog's ids.
        foreach (var chunk in ids.Chunk(1000))
        {
            var part = chunk.ToList();

            if (type == typeof(CatalogPageEntity))
                rows.AddRange(
                    await db
                        .CatalogPages.Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else if (type == typeof(CatalogOfferEntity))
                rows.AddRange(
                    await db
                        .CatalogOffers.Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else if (type == typeof(CatalogProductEntity))
                rows.AddRange(
                    await db
                        .CatalogProducts.Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else
                rows.AddRange(
                    await db
                        .CatalogFeaturedItems.Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
        }

        return rows;
    }

    /// <summary>A row in the editor's words, for a refusal.</summary>
    private static string Describe(CatalogRow row, CatalogRowImage? image)
    {
        var values = image?.Values;

        if (row.Type == typeof(CatalogPageEntity))
            return $"The page {values?.GetValueOrDefault(nameof(CatalogPageEntity.Localization)) ?? row.Id}";

        if (row.Type == typeof(CatalogOfferEntity))
            return $"Offer {row.Id}";

        if (row.Type == typeof(CatalogProductEntity))
            return $"What offer {values?.GetValueOrDefault(nameof(CatalogProductEntity.CatalogOfferEntityId)) ?? "?"} gives";

        return "A featured item";
    }

    private static CatalogRowImage Image(PropertyValues values) =>
        new(values.Properties.ToDictionary(x => x.Name, x => Copy(values[x])));

    public static CatalogRowImage Image(IEntityType type, object entity) =>
        new(
            type.GetProperties()
                .ToDictionary(x => x.Name, x => Copy(x.PropertyInfo?.GetValue(entity)))
        );

    /// <summary>A value to keep: a list (a page's images and texts) is copied, so a later edit can't change it.</summary>
    private static object? Copy(object? value) =>
        value is List<string> list ? new List<string>(list) : value;

    private static bool ValueEquals(object? left, object? right) =>
        left is IEnumerable a and not string && right is IEnumerable b and not string
            ? a.Cast<object?>().SequenceEqual(b.Cast<object?>())
            : Equals(left, right);
}
