using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
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

    /// <summary>How many rows one statement makes, well inside a statement's parameter limit.</summary>
    private const int INSERT_ROWS_MAX = 50;

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
    /// before the step, or to how the step left it - as one transaction. Refused, with why, when
    /// a row is not as the step left it (or found it), or when a row it would take away has
    /// something leaning on it.
    /// <para>
    /// A step can be a whole catalog (a reset, a rollback), so rows are made and taken away in
    /// bulk rather than through the change tracker a row at a time, which against a database
    /// across a network takes minutes; only the rows it changes are tracked.
    /// </para>
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
        var current = new Dictionary<CatalogRow, CatalogRowImage>();

        foreach (var table in rows.GroupBy(x => x.Row.Type))
        {
            var entityType = db.Model.FindEntityType(table.Key)!;
            var ids = table.Select(x => x.Row.Id).ToList();

            foreach (
                var entity in await LoadAsync(db, table.Key, ids, tracked: false, ct)
                    .ConfigureAwait(false)
            )
                current[new CatalogRow(table.Key, ((TurboEntity)entity).Id)] = Image(
                    entityType,
                    entity
                );
        }

        foreach (var (row, expected, _) in rows)
        {
            var found = current.GetValueOrDefault(row);
            var same = found is null
                ? expected is null
                : expected is not null && Same(found, expected);

            if (!same)
                return $"{Describe(row, expected ?? found)} was changed since, so this can't be put back.";
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

        var changing = rows.Where(x =>
                x.Target is not null
                && current.TryGetValue(x.Row, out var found)
                && !Same(found, x.Target)
            )
            .ToList();

        var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false)
            : null;

        try
        {
            // Made first, each table before the ones whose rows name its rows and a page before
            // the pages under it; then changed, now that what they come to name is there; then
            // taken away, the other way round, once nothing kept names them.
            foreach (var type in Tables)
            {
                var made = rows.Where(x =>
                        x.Row.Type == type && x.Target is not null && !current.ContainsKey(x.Row)
                    )
                    .Select(x => x.Target!)
                    .ToList();

                if (type == typeof(CatalogPageEntity))
                    made = ParentsFirst(made);

                await InsertAsync(db, type, made, ct).ConfigureAwait(false);
            }

            foreach (var table in changing.GroupBy(x => x.Row.Type))
            {
                var targets = table.ToDictionary(x => x.Row.Id, x => x.Target!);

                foreach (
                    var entity in await LoadAsync(
                            db,
                            table.Key,
                            [.. targets.Keys],
                            tracked: true,
                            ct
                        )
                        .ConfigureAwait(false)
                )
                {
                    var values = db.Entry(entity).CurrentValues;
                    var target = targets[((TurboEntity)entity).Id];

                    foreach (var property in values.Properties)
                    {
                        if (property.IsPrimaryKey() || property.Name == CREATED_AT)
                            continue;

                        values[property] = Copy(target.Values.GetValueOrDefault(property.Name));
                    }
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            foreach (var type in Tables.Reverse())
            {
                var gone = removing.Where(x => x.Row.Type == type).Select(x => x.Row.Id).ToList();

                if (type == typeof(CatalogPageEntity))
                {
                    foreach (var round in DeepestFirst(gone, current))
                        await DeleteAsync(db, type, round, ct).ConfigureAwait(false);
                }
                else
                    await DeleteAsync(db, type, gone, ct).ConfigureAwait(false);
            }

            if (tx is not null)
                await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync().ConfigureAwait(false);
        }

        // What was written past the tracker is not in it, so nothing it holds is to be trusted.
        db.ChangeTracker.Clear();

        return null;
    }

    /// <summary>Pages to make, each after the page it is under when that is made too.</summary>
    private static List<CatalogRowImage> ParentsFirst(List<CatalogRowImage> pages)
    {
        var byId = pages.ToDictionary(x => x.Id);
        var ordered = new List<CatalogRowImage>(pages.Count);
        var placed = new HashSet<int>();

        foreach (var page in pages)
            Place(page);

        return ordered;

        void Place(CatalogRowImage page)
        {
            if (!placed.Add(page.Id))
                return;

            if (
                page.Values.GetValueOrDefault(nameof(CatalogPageEntity.ParentEntityId))
                    is int parentId
                && byId.TryGetValue(parentId, out var parent)
            )
                Place(parent);

            ordered.Add(page);
        }
    }

    /// <summary>
    /// Pages to take away, in rounds: each round's are under none of the pages still to take, so
    /// the pages under a page go before it.
    /// </summary>
    private static List<List<int>> DeepestFirst(
        List<int> pages,
        Dictionary<CatalogRow, CatalogRowImage> current
    )
    {
        var left = pages.ToHashSet();
        var parentOf = pages.ToDictionary(
            x => x,
            x =>
                current[new CatalogRow(typeof(CatalogPageEntity), x)]
                    .Values.GetValueOrDefault(nameof(CatalogPageEntity.ParentEntityId)) as int?
        );
        var rounds = new List<List<int>>();

        while (left.Count > 0)
        {
            var parents = left.Select(x => parentOf[x]).OfType<int>().ToHashSet();
            var leaves = left.Where(x => !parents.Contains(x)).ToList();

            // A page under itself, which a tree can't have, would otherwise go round forever.
            if (leaves.Count == 0)
                leaves = [.. left];

            rounds.Add(leaves);
            left.ExceptWith(leaves);
        }

        return rounds;
    }

    /// <summary>Rows made as their images have them, ids included, a few hundred to a statement.</summary>
    private static async Task InsertAsync(
        TurboDbContext db,
        Type type,
        List<CatalogRowImage> rows,
        CancellationToken ct
    )
    {
        if (rows.Count == 0)
            return;

        var entityType = db.Model.FindEntityType(type)!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var sql = db.GetService<ISqlGenerationHelper>();
        var columns = entityType
            .GetProperties()
            .Select(x => (Property: x, Column: x.GetColumnName(table)))
            .Where(x => x.Column is not null)
            .ToList();
        var into =
            $"INSERT INTO {sql.DelimitIdentifier(table.Name, table.Schema)} ({string.Join(", ", columns.Select(x => sql.DelimitIdentifier(x.Column!)))}) VALUES ";
        var connection = db.Database.GetDbConnection();

        foreach (var chunk in rows.Chunk(INSERT_ROWS_MAX))
        {
            var text = new StringBuilder(into);
            var parameters = new List<DbParameter>(chunk.Length * columns.Count);
            using var command = connection.CreateCommand();

            for (var r = 0; r < chunk.Length; r++)
            {
                text.Append(r == 0 ? "(" : ", (");

                for (var c = 0; c < columns.Count; c++)
                {
                    var parameter = command.CreateParameter();

                    parameter.ParameterName = $"@p{parameters.Count}";
                    parameter.Value =
                        ToProvider(
                            columns[c].Property,
                            chunk[r].Values.GetValueOrDefault(columns[c].Property.Name)
                        ) ?? DBNull.Value;
                    parameters.Add(parameter);
                    text.Append(c == 0 ? string.Empty : ", ").Append(parameter.ParameterName);
                }

                text.Append(')');
            }

            await db
                .Database.ExecuteSqlRawAsync(text.ToString(), parameters, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>A property's value as its column holds it: a list as its JSON, an enum as its number.</summary>
    private static object? ToProvider(IProperty property, object? value)
    {
        if (value is null)
            return null;

        if (property.GetTypeMapping().Converter is { } converter)
            return converter.ConvertToProvider(value);

        return value is Enum
            ? Convert.ChangeType(
                value,
                Enum.GetUnderlyingType(value.GetType()),
                CultureInfo.InvariantCulture
            )
            : value;
    }

    /// <summary>Rows taken away by id, a thousand to a statement; what cascades goes with them.</summary>
    private static async Task DeleteAsync(
        TurboDbContext db,
        Type type,
        List<int> ids,
        CancellationToken ct
    )
    {
        foreach (var chunk in ids.Chunk(1000))
        {
            var part = chunk.ToList();

            if (type == typeof(CatalogPageEntity))
                await db
                    .CatalogPages.Where(x => part.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
            else if (type == typeof(CatalogOfferEntity))
                await db
                    .CatalogOffers.Where(x => part.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
            else if (type == typeof(CatalogProductEntity))
                await db
                    .CatalogProducts.Where(x => part.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
            else
                await db
                    .CatalogFeaturedItems.Where(x => part.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// What taking rows away would take with it that is not the editor's to take: a limited
    /// series sold from a product, or a page's offers and pages that are staying. Builders Club
    /// furni placed from an offer does not lean on it: it stands on its furniture alone.
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
        bool tracked,
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
                    await Tracked(db.CatalogPages)
                        .Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else if (type == typeof(CatalogOfferEntity))
                rows.AddRange(
                    await Tracked(db.CatalogOffers)
                        .Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else if (type == typeof(CatalogProductEntity))
                rows.AddRange(
                    await Tracked(db.CatalogProducts)
                        .Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
            else
                rows.AddRange(
                    await Tracked(db.CatalogFeaturedItems)
                        .Where(x => part.Contains(x.Id))
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                );
        }

        return rows;

        IQueryable<T> Tracked<T>(DbSet<T> set)
            where T : class => tracked ? set : set.AsNoTracking();
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
