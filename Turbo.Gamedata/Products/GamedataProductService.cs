using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Products;

/// <summary>
/// The hotel's product data (<see cref="IGamedataProductService"/>). An import compares each of
/// Habbo's products field by field - name and description - three ways, as a text is compared
/// (<see cref="GamedataTextService.Decide"/>): Habbo's new value, Habbo's when last taken in
/// (<c>habbo_products</c>), and the hotel's (<c>gamedata_products</c>). A product the hotel removed
/// stays removed unless Habbo changes it; one Habbo drops stays. A change records
/// <c>{ code, name, description }</c> before and after.
/// </summary>
internal sealed class GamedataProductService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    TimeProvider time,
    ILogger<GamedataProductService> logger
)
    : HabboMirroredService<
        HabboProductVersionEntity,
        GamedataProductEntity,
        HabboProductEntity,
        GamedataProductService.PlanItem,
        ProductImportPreview
    >(dbCtxFactory, config, writes, time, logger),
        IGamedataProductService
{
    public const string NAME = "name";
    public const string DESCRIPTION = "description";
    public const string CODE = "code";

    protected override string What => "product data";

    protected override ProductImportPreview Preview(
        HabboProductVersionEntity version,
        List<PlanItem> plan,
        IEnumerable<PlanItem> shown,
        bool truncated
    ) =>
        new()
        {
            Version = version.ToSnapshot(),
            Added = Count(plan, FurnitureImportAction.Add),
            Updated = Count(plan, FurnitureImportAction.Update),
            Kept = Count(plan, FurnitureImportAction.Keep),
            Unchanged = Count(plan, null),
            Items =
            [
                .. shown.Select(x => new ProductImportItem
                {
                    Code = x.Code,
                    Action = x.Action!.Value,
                    Fields = x.Fields,
                }),
            ],
            Truncated = truncated,
        };

    protected override (GamedataProductEntity Row, GamedataChangeEntity Change) AddOurs(
        PlanItem item
    ) =>
        (
            new GamedataProductEntity
            {
                Code = item.Code,
                Name = item.HabboName,
                Description = item.HabboDescription,
            },
            Change(
                GamedataRecordType.Product,
                item.Code,
                null,
                (item.HabboName, item.HabboDescription)
            )
        );

    protected override GamedataChangeEntity UpdateOurs(PlanItem item, GamedataProductEntity row)
    {
        var before = (row.Name, row.Description);

        foreach (var field in item.Fields.Where(x => !x.Kept))
        {
            if (field.Field == NAME)
                row.Name = item.HabboName;
            else
                row.Description = item.HabboDescription;
        }

        return Change(GamedataRecordType.Product, item.Code, before, (row.Name, row.Description));
    }

    protected override GamedataChangeEntity? TakeHabbo(
        PlanItem item,
        HabboProductEntity known,
        int versionId
    )
    {
        var habbo = (item.HabboName, item.HabboDescription);

        if ((known.Name, known.Description) == habbo)
            return null;

        var change = Change(
            GamedataRecordType.HabboProduct,
            item.Code,
            (known.Name, known.Description),
            habbo
        );

        known.Name = item.HabboName;
        known.Description = item.HabboDescription;
        known.HabboProductVersionEntityId = versionId;

        return change;
    }

    protected override (HabboProductEntity Row, GamedataChangeEntity Change) AddHabbo(
        PlanItem item,
        int versionId
    ) =>
        (
            new HabboProductEntity
            {
                Code = item.Code,
                Name = item.HabboName,
                Description = item.HabboDescription,
                HabboProductVersionEntityId = versionId,
            },
            Change(
                GamedataRecordType.HabboProduct,
                item.Code,
                null,
                (item.HabboName, item.HabboDescription)
            )
        );

    protected override string ImportSummary(
        HabboProductVersionEntity version,
        int added,
        int updated,
        int kept
    ) =>
        $"Habbo's product data ({version.ProductCount}): {added} added, {updated} updated, {kept} kept as the hotel has them";

    protected override void Changed() => files.Invalidate(GamedataFiles.PRODUCT_DATA);

    public async Task<ProductSearchResult> SearchAsync(
        string? query,
        int page,
        CancellationToken ct
    )
    {
        var size = Math.Max(1, Config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var products = dbCtx.GamedataProducts.AsNoTracking();

        if (words.Length > 0)
            products = products.Where(x =>
                x.Code.Contains(words)
                || (x.Name != null && x.Name.Contains(words))
                || (x.Description != null && x.Description.Contains(words))
            );

        var total = await products.CountAsync(ct).ConfigureAwait(false);
        var rows = await products
            .OrderBy(x => x.Code)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ProductSearchResult
        {
            Items = await EntriesAsync(dbCtx, rows, ct).ConfigureAwait(false),
            Total = total,
            PageSize = size,
        };
    }

    public async Task<ImmutableArray<ProductEntrySnapshot>> LookupAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken ct
    )
    {
        var wanted = codes.Where(x => x.Length > 0).Distinct().Take(Config.TextPageSize).ToList();

        if (wanted.Count == 0)
            return [];

        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataProducts.AsNoTracking()
            .Where(x => wanted.Contains(x.Code))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return await EntriesAsync(dbCtx, rows, ct).ConfigureAwait(false);
    }

    public Task<ProductEntrySnapshot> SaveAsync(
        string code,
        string? name,
        string? description,
        PlayerId player,
        CancellationToken ct
    ) => Writes.RunAsync(() => SaveLockedAsync(code, name, description, player, ct), ct);

    private async Task<ProductEntrySnapshot> SaveLockedAsync(
        string code,
        string? name,
        string? description,
        PlayerId player,
        CancellationToken ct
    )
    {
        code = CheckCode(code);

        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataProducts.FirstOrDefaultAsync(x => x.Code == code, ct)
            .ConfigureAwait(false);
        (string?, string?)? before = row is null ? null : (row.Name, row.Description);

        if (row is null)
        {
            row = new GamedataProductEntity { Code = code };
            dbCtx.GamedataProducts.Add(row);
        }

        row.Name = name;
        row.Description = description;

        if (before != (name, description))
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            dbCtx.GamedataChangeSets.Add(
                GamedataChangeSets.Create(
                    GamedataChangeKind.Edit,
                    before is null ? $"Added the product {code}" : $"Edited the product {code}",
                    player,
                    [Change(GamedataRecordType.Product, code, before, (name, description), row.Id)]
                )
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            Changed();
        }

        return (await EntriesAsync(dbCtx, [row], ct).ConfigureAwait(false))[0];
    }

    public Task<bool> DeleteAsync(string code, PlayerId player, CancellationToken ct) =>
        RemoveAsync(
            x => x.Code == code,
            row => Change(GamedataRecordType.Product, code, (row.Name, row.Description), null),
            $"Removed the product {code}",
            player,
            ct
        );

    /// <summary>A code an offer's name key can be: not empty, and within the column.</summary>
    internal static string CheckCode(string code)
    {
        code = code.Trim();

        if (code.Length == 0)
            throw new ArgumentException("A product needs a code.", nameof(code));

        if (code.Length > GamedataProductEntity.CODE_MAX_LENGTH)
            throw new ArgumentException(
                $"A code can be at most {GamedataProductEntity.CODE_MAX_LENGTH} characters.",
                nameof(code)
            );

        return code;
    }

    /// <summary>Products as entries, with Habbo's as last taken in beside each.</summary>
    private static async Task<ImmutableArray<ProductEntrySnapshot>> EntriesAsync(
        TurboDbContext dbCtx,
        IReadOnlyCollection<GamedataProductEntity> rows,
        CancellationToken ct
    )
    {
        var codes = rows.Select(x => x.Code).ToList();
        var habbo = await dbCtx
            .HabboProducts.AsNoTracking()
            .Where(x => codes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x =>
            {
                habbo.TryGetValue(x.Code, out var known);

                return new ProductEntrySnapshot
                {
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    FromHabbo = known is not null,
                    HabboName = known?.Name,
                    HabboDescription = known?.Description,
                };
            }),
        ];
    }

    protected override async Task<List<PlanItem>> PlanAsync(
        TurboDbContext dbCtx,
        HabboProductVersionEntity version,
        bool tracked,
        CancellationToken ct
    )
    {
        var oursQuery = dbCtx.GamedataProducts.AsQueryable();
        var basesQuery = dbCtx.HabboProducts.AsQueryable();

        if (!tracked)
        {
            oursQuery = oursQuery.AsNoTracking();
            basesQuery = basesQuery.AsNoTracking();
        }

        var ours = await oursQuery
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var bases = await basesQuery
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var habbo = ProductDataFile.Parse(GamedataBytes.Decompress(version.Content));
        var plan = new List<PlanItem>();

        foreach (var (code, (name, description)) in habbo)
        {
            if (code.Length > GamedataProductEntity.CODE_MAX_LENGTH)
                continue;

            ours.TryGetValue(code, out var current);
            bases.TryGetValue(code, out var known);

            var (action, fields) = Decide(
                (name, description),
                current is null ? null : (current.Name, current.Description),
                known is null ? null : (known.Name, known.Description)
            );

            plan.Add(new PlanItem(code, name, description, current, known, action, fields));
        }

        return plan;
    }

    /// <summary>
    /// What an import does with one product, from Habbo's values, the hotel's (null: it has none)
    /// and Habbo's last (null: never taken in). Each field is decided as a text is.
    /// </summary>
    internal static (
        FurnitureImportAction? Action,
        ImmutableArray<FurnitureFieldChange> Fields
    ) Decide(
        (string? Name, string? Description) habbo,
        (string? Name, string? Description)? ours,
        (string? Name, string? Description)? previous
    )
    {
        if (ours is null)
        {
            if (previous is null)
                return (
                    FurnitureImportAction.Add,
                    [
                        Field(NAME, null, habbo.Name, false),
                        Field(DESCRIPTION, null, habbo.Description, false),
                    ]
                );

            // The hotel removed it: worth saying only when Habbo changed it since.
            return habbo == previous.Value
                ? (null, [])
                : (
                    FurnitureImportAction.Keep,
                    [
                        Field(NAME, null, habbo.Name, true),
                        Field(DESCRIPTION, null, habbo.Description, true),
                    ]
                );
        }

        var fields = ImmutableArray.CreateBuilder<FurnitureFieldChange>();

        foreach (
            var (key, incoming, current, before) in new[]
            {
                (NAME, habbo.Name, ours.Value.Name, previous?.Name),
                (DESCRIPTION, habbo.Description, ours.Value.Description, previous?.Description),
            }
        )
        {
            if (incoming == current)
                continue;

            // Never taken in, but the hotel has it: the hotel's own.
            var action = previous is null
                ? FurnitureImportAction.Keep
                : GamedataTextService.Decide(
                    incoming ?? string.Empty,
                    current ?? string.Empty,
                    before ?? string.Empty
                );

            if (action is null)
                continue;

            fields.Add(Field(key, current, incoming, action == FurnitureImportAction.Keep));
        }

        return fields.Count == 0 ? (null, [])
            : fields.Any(x => !x.Kept) ? (FurnitureImportAction.Update, fields.ToImmutable())
            : (FurnitureImportAction.Keep, fields.ToImmutable());
    }

    private static FurnitureFieldChange Field(
        string key,
        string? current,
        string? incoming,
        bool kept
    ) =>
        new()
        {
            Field = key,
            Current = Json(current),
            Incoming = Json(incoming),
            Kept = kept,
        };

    private static string Json(string? value) =>
        value is null ? "null" : JsonValue.Create(value).ToJsonString();

    /// <param name="recordId">The product's row; 0 for one not saved yet.</param>
    private static GamedataChangeEntity Change(
        GamedataRecordType type,
        string code,
        (string? Name, string? Description)? before,
        (string? Name, string? Description)? after,
        int recordId = 0
    ) =>
        GamedataChangeSets.Change(
            type,
            code,
            before is { } b ? Record(code, b.Name, b.Description) : null,
            after is { } a ? Record(code, a.Name, a.Description) : null,
            recordId
        );

    internal static string Record(string code, string? name, string? description) =>
        new JsonObject
        {
            [CODE] = code,
            [NAME] = name,
            [DESCRIPTION] = description,
        }.ToJsonString();

    internal sealed record PlanItem(
        string Code,
        string? HabboName,
        string? HabboDescription,
        GamedataProductEntity? Current,
        HabboProductEntity? Base,
        FurnitureImportAction? Action,
        ImmutableArray<FurnitureFieldChange> Fields
    ) : IHabboImportItem<GamedataProductEntity, HabboProductEntity>;
}
