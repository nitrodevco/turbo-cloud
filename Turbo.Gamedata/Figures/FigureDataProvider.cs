using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Figures;

/// <summary>
/// <see cref="IFigureDataProvider"/> from <c>gamedata_figure_records</c>: read whole when first
/// asked for - every figure change is checked against it - and kept
/// <see cref="GamedataConfig.FigureCacheSeconds"/>, so another server's edit reaches this one in
/// that time. An edit or import here forgets it at once.
/// </summary>
internal sealed class FigureDataProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    TimeProvider time,
    ILogger<FigureDataProvider> logger
) : IFigureDataProvider
{
    private readonly SemaphoreSlim _loading = new(1, 1);
    private Loaded? _loaded;

    public async Task<FigureData> GetAsync(CancellationToken ct)
    {
        if (_loaded is { } loaded && loaded.Until > time.GetUtcNow())
            return loaded.Data;

        await _loading.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_loaded is { } again && again.Until > time.GetUtcNow())
                return again.Data;

            var data = await LoadAsync(ct).ConfigureAwait(false);

            _loaded = new Loaded(
                data,
                time.GetUtcNow().AddSeconds(Math.Max(0, config.Value.FigureCacheSeconds))
            );

            return data;
        }
        finally
        {
            _loading.Release();
        }
    }

    public void Invalidate() => _loaded = null;

    private async Task<FigureData> LoadAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataFigures.AsNoTracking()
            .Select(x => new { x.Kind, x.Data })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var records = new List<(FigureRecordKind Kind, JsonObject Record)>();

        foreach (var row in rows)
        {
            try
            {
                records.Add(
                    (row.Kind, FigureRecords.Normalize(row.Kind, FigureRecords.Parse(row.Data)))
                );
            }
            catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
            {
                logger.LogWarning(
                    ex,
                    "Skipping a figure record that doesn't read: {Data}",
                    row.Data
                );
            }
        }

        return Build(records);
    }

    /// <summary>The figure data from records in their one shape (<see cref="FigureRecords.Normalize"/>).</summary>
    internal static FigureData Build(
        IEnumerable<(FigureRecordKind Kind, JsonObject Record)> records
    )
    {
        var palettes = new Dictionary<int, Dictionary<int, FigureColor>>();
        var types = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var sets = new Dictionary<string, Dictionary<int, FigureSet>>(StringComparer.Ordinal);

        foreach (var (kind, record) in records)
        {
            switch (kind)
            {
                case FigureRecordKind.Color:
                    var paletteId = Int(record, FigureRecords.PALETTE);

                    if (!palettes.TryGetValue(paletteId, out var colors))
                        palettes[paletteId] = colors = [];

                    colors[Int(record, FigureRecords.ID)] = new FigureColor(
                        Int(record, FigureRecords.ID),
                        Int(record, FigureRecords.INDEX),
                        Int(record, FigureRecords.CLUB),
                        Bool(record, FigureRecords.SELECTABLE)
                    );

                    break;
                case FigureRecordKind.SetType:
                    types[Str(record, FigureRecords.TYPE)] = record;

                    break;
                case FigureRecordKind.Set:
                    var type = Str(record, FigureRecords.TYPE);

                    if (!sets.TryGetValue(type, out var ofType))
                        sets[type] = ofType = [];

                    var parts = record[FigureRecords.PARTS] as JsonArray ?? [];
                    var layers = parts
                        .OfType<JsonObject>()
                        .Select(x => Int(x, FigureRecords.COLOR_INDEX))
                        .DefaultIfEmpty(0)
                        .Max();

                    ofType[Int(record, FigureRecords.ID)] = new FigureSet(
                        Int(record, FigureRecords.ID),
                        Str(record, FigureRecords.GENDER)[0],
                        Int(record, FigureRecords.CLUB),
                        Bool(record, FigureRecords.COLORABLE),
                        Bool(record, FigureRecords.SELECTABLE),
                        Bool(record, FigureRecords.PRESELECTABLE),
                        Bool(record, FigureRecords.SELLABLE),
                        Math.Max(1, layers)
                    );

                    break;
            }
        }

        // A piece whose kind isn't listed can't be drawn: the client knows it by its kind.
        return new FigureData(
            types.ToDictionary(
                x => x.Key,
                x => new FigureSetType(
                    x.Key,
                    Int(x.Value, FigureRecords.PALETTE_ID),
                    Bool(x.Value, FigureRecords.MAND_M_0),
                    Bool(x.Value, FigureRecords.MAND_F_0),
                    Bool(x.Value, FigureRecords.MAND_M_1),
                    Bool(x.Value, FigureRecords.MAND_F_1),
                    sets.GetValueOrDefault(x.Key) ?? []
                ),
                StringComparer.Ordinal
            ),
            palettes.ToDictionary(x => x.Key, x => new FigurePalette(x.Key, x.Value))
        );
    }

    private static int Int(JsonObject record, string field) => record[field]!.GetValue<int>();

    private static bool Bool(JsonObject record, string field) => record[field]!.GetValue<bool>();

    private static string Str(JsonObject record, string field) => record[field]!.GetValue<string>();

    private sealed record Loaded(FigureData Data, DateTimeOffset Until);
}
