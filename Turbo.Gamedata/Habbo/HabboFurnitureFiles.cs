using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Furniture;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Furniture;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Habbo's furniture asset files, read for what furnidata does not say (<see cref="FurnitureAssetInfo"/>):
/// downloaded once per asset and revision and kept in <c>habbo_furniture_assets</c>. A furniture's
/// colours share one file. What the file says reaches an import as extra fields of Habbo's item
/// (<see cref="Enrich"/>): <c>states</c> sets a definition's <c>total_states</c>.
/// </summary>
internal sealed class HabboFurnitureFiles(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    HabboGamedataClient habbo,
    ILogger<HabboFurnitureFiles> logger
)
{
    /// <summary>The field an asset file gives an item: its states (<c>total_states</c>).</summary>
    public const string STATES_KEY = "states";

    // Rows are written in batches: a first import reads every file Habbo has.
    private const int SAVE_BATCH = 200;

    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    /// <summary>The asset a furnidata item's file is: its classname without its <c>*N</c> colour.</summary>
    public static string AssetName(string className)
    {
        var star = className.IndexOf('*', StringComparison.Ordinal);

        return star < 0 ? className : className[..star];
    }

    /// <summary>The revision furnidata gives an item, the folder its file is in.</summary>
    public static int Revision(JsonObject item) =>
        FurnitureValues.ToInt(item["revision"], "revision");

    /// <summary>Every file read, by asset and revision.</summary>
    public async Task<
        Dictionary<(string Asset, int Revision), HabboFurnitureAssetEntity>
    > LoadAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .HabboFurnitureAssets.AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.GroupBy(x => (x.AssetName, x.Revision))
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>The files of these items not read yet, or whose download failed before.</summary>
    public static List<(string Asset, int Revision)> Missing(
        IEnumerable<JsonObject> items,
        IReadOnlyDictionary<(string Asset, int Revision), HabboFurnitureAssetEntity> known
    ) =>
        [
            .. items
                .Select(item =>
                    (
                        AssetName(item["classname"]?.GetValue<string>() ?? string.Empty),
                        Revision(item)
                    )
                )
                .Where(x => x.Item1.Length > 0)
                .Distinct()
                .Where(x =>
                    !known.TryGetValue(x, out var row) || (row.Error is not null && row.Retry)
                ),
        ];

    /// <summary>
    /// Downloads and reads these files, <see cref="GamedataConfig.FurnitureFileConcurrency"/> at a
    /// time, and keeps what each said - or why it could not be read. <paramref name="progress"/>
    /// is told each file done, and whether it failed.
    /// </summary>
    public async Task FetchAsync(
        IReadOnlyCollection<(string Asset, int Revision)> files,
        Action<bool> progress,
        CancellationToken ct
    )
    {
        // Workers read files; one writer keeps them as they come, so a long first import that
        // stops part way keeps what it read.
        var read = Channel.CreateBounded<HabboFurnitureAssetEntity>(SAVE_BATCH * 2);
        var writer = WriteAsync(read.Reader);

        try
        {
            await Parallel
                .ForEachAsync(
                    files,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, config.Value.FurnitureFileConcurrency),
                        CancellationToken = ct,
                    },
                    async (file, token) =>
                    {
                        var row = await ReadAsync(file.Asset, file.Revision, token)
                            .ConfigureAwait(false);

                        await read.Writer.WriteAsync(row, token).ConfigureAwait(false);
                        progress(row.Error is not null);
                    }
                )
                .ConfigureAwait(false);
        }
        finally
        {
            read.Writer.TryComplete();
        }

        await writer.ConfigureAwait(false);
    }

    private async Task WriteAsync(ChannelReader<HabboFurnitureAssetEntity> rows)
    {
        var batch = new List<HabboFurnitureAssetEntity>(SAVE_BATCH);

        // Not the caller's token: what was read is kept even when the fetch is stopped.
        await foreach (var row in rows.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            batch.Add(row);

            if (batch.Count < SAVE_BATCH)
                continue;

            await SaveAsync(batch, CancellationToken.None).ConfigureAwait(false);
            batch.Clear();
        }

        await SaveAsync(batch, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// The item with what its file said as extra fields, when its file was read: a copy, so the
    /// item stays as Habbo wrote it. The item itself when its file was not read.
    /// </summary>
    public static JsonObject Enrich(
        JsonObject item,
        IReadOnlyDictionary<(string Asset, int Revision), HabboFurnitureAssetEntity> files
    )
    {
        var className = item["classname"]?.GetValue<string>();

        if (
            className is null
            || !files.TryGetValue((AssetName(className), Revision(item)), out var file)
            || file.Error is not null
        )
            return item;

        var enriched = (JsonObject)item.DeepClone();

        enriched[STATES_KEY] = file.States;

        return enriched;
    }

    private async Task<HabboFurnitureAssetEntity> ReadAsync(
        string asset,
        int revision,
        CancellationToken ct
    )
    {
        var url = config
            .Value.FurnitureFileUrl.Replace(
                "{domain}",
                config.Value.HabboDomain,
                StringComparison.Ordinal
            )
            .Replace(
                "{revision}",
                revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            )
            .Replace("{name}", Uri.EscapeDataString(asset), StringComparison.Ordinal);
        var row = new HabboFurnitureAssetEntity { AssetName = asset, Revision = revision };

        try
        {
            var data = await habbo.GetFurnitureFileAsync(url, ct).ConfigureAwait(false);

            if (data is null)
            {
                row.Error = "Habbo has no file at its address.";

                return row;
            }

            var info = FurnitureAssetReader.Read(data, asset);

            row.States = info.States;
            row.LogicType = Truncate(info.Logic, 64);
            row.VisualizationType = Truncate(info.Visualization, 64);
            row.Info = JsonSerializer.Serialize(info, JSON);
        }
        catch (AssetFormatException ex)
        {
            row.Error = Truncate(ex.Message, HabboFurnitureAssetEntity.ERROR_MAX_LENGTH);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Furniture file {Asset} ({Revision}) could not be downloaded",
                asset,
                revision
            );
            row.Error = Truncate(ex.Message, HabboFurnitureAssetEntity.ERROR_MAX_LENGTH);
            row.Retry = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One file the reader trips on must not stop the rest of an import: it is kept as
            // failed, logged, and tried again by the next one.
            logger.LogError(
                ex,
                "Furniture file {Asset} ({Revision}) could not be read",
                asset,
                revision
            );
            row.Error = Truncate(
                $"Could not be read: {ex.Message}",
                HabboFurnitureAssetEntity.ERROR_MAX_LENGTH
            );
            row.Retry = true;
        }

        return row;
    }

    /// <summary>Keeps the rows read, replacing earlier failures of the same files.</summary>
    private async Task SaveAsync(List<HabboFurnitureAssetEntity> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var names = rows.Select(x => x.AssetName).Distinct().ToList();
        var existing = await dbCtx
            .HabboFurnitureAssets.Where(x => names.Contains(x.AssetName))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var byKey = existing.ToDictionary(x => (x.AssetName, x.Revision));

        foreach (var row in rows)
        {
            if (byKey.TryGetValue((row.AssetName, row.Revision), out var known))
            {
                known.States = row.States;
                known.LogicType = row.LogicType;
                known.VisualizationType = row.VisualizationType;
                known.Info = row.Info;
                known.Error = row.Error;
                known.Retry = row.Retry;
            }
            else
            {
                dbCtx.HabboFurnitureAssets.Add(row);
            }
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max];
}
