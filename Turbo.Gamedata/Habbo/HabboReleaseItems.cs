using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// The furniture of Habbo's newest release found, by kind and classname: Habbo's values as it
/// serves them now, whether or not the release was taken in. Its file is large, so it is read
/// once per release and held.
/// </summary>
internal sealed class HabboReleaseItems(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config
)
{
    private static readonly (string List, ProductType Type)[] LISTS =
    [
        ("roomitemtypes", ProductType.Floor),
        ("wallitemtypes", ProductType.Wall),
    ];

    private readonly SemaphoreSlim _loading = new(1, 1);
    private Loaded? _loaded;

    /// <summary>The newest release's items; null before Habbo was first checked.</summary>
    public async Task<Loaded?> GetLatestAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var domain = config.Value.HabboDomain;
        var latestId = await dbCtx
            .HabboReleases.Where(x => x.Domain == domain)
            .OrderByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (latestId is not { } id)
            return null;

        if (_loaded is { } loaded && loaded.Release.Id == id)
            return loaded;

        await _loading.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_loaded is { } done && done.Release.Id == id)
                return done;

            var release = await dbCtx
                .HabboReleases.AsNoTracking()
                .FirstAsync(x => x.Id == id, ct)
                .ConfigureAwait(false);
            var items = Parse(release.FurnitureData);

            _loaded = new Loaded(release.ToSnapshot(), items);

            return _loaded;
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>A release's furniture data (gzipped, as kept) by kind and classname.</summary>
    public static Dictionary<(ProductType Type, string ClassName), JsonObject> Parse(byte[] gzipped)
    {
        var data = JsonNode.Parse(GamedataBytes.Decompress(gzipped));
        var items = new Dictionary<(ProductType, string), JsonObject>();

        foreach (var (list, type) in LISTS)
        {
            if (data?[list]?["furnitype"] is not JsonArray array)
                continue;

            foreach (var node in array)
            {
                if (
                    node is not JsonObject item
                    || item["classname"]?.GetValue<string>() is not { Length: > 0 } className
                )
                    continue;

                // A JsonObject builds its lookup on first read, which is not safe to race;
                // built here, the items are only ever read after.
                _ = item.Count;
                _ = item.ContainsKey("id");

                items[(type, className)] = item;
            }
        }

        return items;
    }

    public sealed record Loaded(
        HabboReleaseSnapshot Release,
        IReadOnlyDictionary<(ProductType Type, string ClassName), JsonObject> Items
    );
}
