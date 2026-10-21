using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Guilds;
using Turbo.Gamedata.Products;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Settings;

namespace Turbo.Gamedata.Files;

/// <summary>
/// The gamedata files built from the database (<see cref="IGamedataFileService"/>): FurnitureData
/// from the definitions and the catalogs, the external texts from <c>gamedata_texts</c>, the
/// external variables from <c>gamedata_variables</c>, each that follows a server setting or another
/// file's address written with it, and the figure and effect maps from the clothing and effect
/// bundles (<see cref="ClientMaps"/>), built again whenever one of those is kept, changed or removed.
/// Each file's current build is held in memory and built again when what it is made from
/// changes - a catalog snapshot replaced (a catalog published), another file's hash (for the
/// variables), its rows said to have changed (<see cref="Invalidate"/>), or the database saying they
/// changed (<see cref="RowsStamp"/>), asked at most every <see cref="GamedataConfig.FileCheckSeconds"/>
/// so an edit made on another silo reaches this one's files too. Each build is kept in <c>gamedata_builds</c> by its hash, the
/// newest <see cref="GamedataConfig.KeepBuilds"/> of a file besides the current one, so a client
/// that loaded an address before a rebuild still finds it. The same content always hashes the
/// same, so every silo names a build alike.
/// </summary>
internal sealed class GamedataFileService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    FurnitureOfferCatalog offers,
    IServerSettings settings,
    IAssetBundleStore store,
    TimeProvider time,
    ILogger<GamedataFileService> logger
) : IGamedataFileService
{
    /// <summary>The parts read from a clothing bundle's file, by its name and hash, for one no map lists yet.</summary>
    private readonly ConcurrentDictionary<(string Name, string Hash), string> _fileParts = new();

    private readonly GamedataConfig _config = config.Value;
    private readonly SemaphoreSlim _building = new(1, 1);
    private readonly ConcurrentDictionary<string, Build> _current = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _versions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Stamped> _stamps = new(StringComparer.Ordinal);

    public async Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct)
    {
        RequireKnown(file);

        var inputs = await InputsAsync(file, ct).ConfigureAwait(false);
        var stamp = await StampAsync(file, ct).ConfigureAwait(false);

        if (
            _current.TryGetValue(file, out var current)
            && current.IsFor(VersionOf(file), inputs, stamp)
        )
            return current.Content;

        return await BuildAsync(file, force: false, ct).ConfigureAwait(false);
    }

    public async Task<GamedataFileContent?> GetAsync(string file, string hash, CancellationToken ct)
    {
        if (!GamedataFiles.IsKnown(file))
            return null;

        var current = await GetCurrentAsync(file, ct).ConfigureAwait(false);

        if (current.File.Hash == hash)
            return current;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var kept = await dbCtx
            .GamedataBuilds.AsNoTracking()
            .FirstOrDefaultAsync(x => x.File == file && x.Hash == hash, ct)
            .ConfigureAwait(false);

        return kept is null ? null : new GamedataFileContent(kept.ToSnapshot(), kept.Content);
    }

    public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct)
    {
        RequireKnown(file);

        return BuildAsync(file, force: true, ct);
    }

    public void Invalidate(string file)
    {
        _stamps.TryRemove(file, out _);
        _versions.AddOrUpdate(file, 1, (_, version) => version + 1);
    }

    private int VersionOf(string file) =>
        _versions.TryGetValue(file, out var version) ? version : 0;

    /// <summary>
    /// What a file is made from besides its rows: the catalogs, for FurnitureData; the other
    /// files' addresses and the settings' version, for the external variables. Worked out outside
    /// the build lock, as the addresses may build their files.
    /// </summary>
    private async Task<object?> InputsAsync(string file, CancellationToken ct) =>
        file switch
        {
            GamedataFiles.FURNITURE_DATA => await offers.GetAsync(ct).ConfigureAwait(false),
            GamedataFiles.EXTERNAL_VARIABLES => new ExternalVariablesFile.Inputs(
                await ExternalVariablesFile
                    .AddressesAsync(this, _config.PublicUrl, ct)
                    .ConfigureAwait(false),
                settings.Version
            ),
            GamedataFiles.FIGURE_MAP => await BundlesStampAsync(AssetBundleKind.Figure, ct)
                .ConfigureAwait(false),
            GamedataFiles.EFFECT_MAP => await BundlesStampAsync(AssetBundleKind.Effect, ct)
                .ConfigureAwait(false),
            _ => null,
        };

    /// <summary>
    /// What a map is built from, cheaply: how many bundles of the kind have a file and when one
    /// was last kept. A bundle kept, changed (its map entries too) or removed changes it.
    /// </summary>
    private async Task<BundlesStamp> BundlesStampAsync(AssetBundleKind kind, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var bundles = dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == kind && x.Hash != null);

        return new BundlesStamp(
            await bundles.CountAsync(ct).ConfigureAwait(false),
            await bundles.MaxAsync(x => (DateTime?)x.UpdatedAt, ct).ConfigureAwait(false)
        );
    }

    private sealed record BundlesStamp(int Count, DateTime? Latest);

    /// <summary>
    /// What the file's rows are, as the database says it (<see cref="ReadStampAsync"/>), kept for
    /// <see cref="GamedataConfig.FileCheckSeconds"/> so a request rarely asks. An edit on this silo
    /// forgets it (<see cref="Invalidate"/>); one on another is seen once it is asked again.
    /// </summary>
    private async Task<object?> StampAsync(string file, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        if (
            _stamps.TryGetValue(file, out var known)
            && now - known.At < TimeSpan.FromSeconds(_config.FileCheckSeconds)
        )
            return known.Stamp;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var stamp = await ReadStampAsync(dbCtx, file, ct).ConfigureAwait(false);

        _stamps[file] = new Stamped(stamp, now);

        return stamp;
    }

    /// <summary>
    /// What a file's rows are, as cheaply as the database can say it (<see cref="RowsStamp"/>).
    /// The badge parts are edited without a change set, and are few enough to read whole. The
    /// maps' bundles are read for every request already (<see cref="InputsAsync"/>).
    /// </summary>
    private static async Task<object?> ReadStampAsync(
        TurboDbContext dbCtx,
        string file,
        CancellationToken ct
    ) =>
        file switch
        {
            GamedataFiles.FURNITURE_DATA => await RowsStamp
                .ReadAsync(dbCtx, dbCtx.FurnitureDefinitions, ct)
                .ConfigureAwait(false),
            GamedataFiles.PRODUCT_DATA => await RowsStamp
                .ReadAsync(dbCtx, dbCtx.GamedataProducts, ct)
                .ConfigureAwait(false),
            GamedataFiles.FIGURE_DATA => await RowsStamp
                .ReadAsync(dbCtx, dbCtx.GamedataFigures, ct)
                .ConfigureAwait(false),
            GamedataFiles.EXTERNAL_TEXTS => await RowsStamp
                .ReadAsync(dbCtx, dbCtx.GamedataTexts, ct)
                .ConfigureAwait(false),
            GamedataFiles.EXTERNAL_VARIABLES => await RowsStamp
                .ReadAsync(dbCtx, dbCtx.GamedataVariables, ct)
                .ConfigureAwait(false),
            GamedataFiles.BADGE_PARTS => GamedataBytes.Hash(
                (await BadgePartsAsync(dbCtx, ct).ConfigureAwait(false)).Content
            ),
            _ => null,
        };

    /// <summary>
    /// A file's rows as the database says it: the newest change set (every import, edit and
    /// rollback the gamedata makes is one), and how many rows there are and the newest, for rows
    /// added or removed without one.
    /// </summary>
    private sealed record RowsStamp(int? ChangeSet, int Rows, int? MaxId)
    {
        public static async Task<RowsStamp> ReadAsync<T>(
            TurboDbContext dbCtx,
            IQueryable<T> rows,
            CancellationToken ct
        )
            where T : TurboEntity
        {
            var ids = rows.AsNoTracking().Select(x => x.Id);

            return new RowsStamp(
                await dbCtx
                    .GamedataChangeSets.AsNoTracking()
                    .MaxAsync(x => (int?)x.Id, ct)
                    .ConfigureAwait(false),
                await ids.CountAsync(ct).ConfigureAwait(false),
                await ids.MaxAsync(x => (int?)x, ct).ConfigureAwait(false)
            );
        }
    }

    private sealed record Stamped(object? Stamp, DateTimeOffset At);

    private async Task<GamedataFileContent> BuildAsync(
        string file,
        bool force,
        CancellationToken ct
    )
    {
        var inputs = await InputsAsync(file, ct).ConfigureAwait(false);
        var stamp = await StampAsync(file, ct).ConfigureAwait(false);

        await _building.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // Whoever waited behind a build that has just finished takes that build.
            var version = VersionOf(file);

            if (
                !force
                && _current.TryGetValue(file, out var built)
                && built.IsFor(version, inputs, stamp)
            )
                return built.Content;

            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var (content, count) = file switch
            {
                GamedataFiles.FURNITURE_DATA => await FurnitureDataAsync(
                        dbCtx,
                        (FurnitureOfferCatalog.Stamps)inputs!,
                        ct
                    )
                    .ConfigureAwait(false),
                GamedataFiles.PRODUCT_DATA => await ProductDataAsync(dbCtx, ct)
                    .ConfigureAwait(false),
                GamedataFiles.FIGURE_DATA => await FigureDataAsync(dbCtx, ct).ConfigureAwait(false),
                GamedataFiles.BADGE_PARTS => await BadgePartsAsync(dbCtx, ct).ConfigureAwait(false),
                GamedataFiles.FIGURE_MAP => await FigureMapAsync(dbCtx, ct).ConfigureAwait(false),
                GamedataFiles.EFFECT_MAP => await EffectMapAsync(dbCtx, ct).ConfigureAwait(false),
                GamedataFiles.EXTERNAL_VARIABLES => await ExternalVariablesAsync(
                        dbCtx,
                        ((ExternalVariablesFile.Inputs)inputs!).Addresses,
                        ct
                    )
                    .ConfigureAwait(false),
                _ => await ExternalTextsAsync(dbCtx, ct).ConfigureAwait(false),
            };
            var hash = GamedataBytes.Hash(content);

            var row = await dbCtx
                .GamedataBuilds.AsNoTracking()
                .FirstOrDefaultAsync(x => x.File == file && x.Hash == hash, ct)
                .ConfigureAwait(false);

            if (row is null)
            {
                row = new GamedataBuildEntity
                {
                    File = file,
                    Hash = hash,
                    Content = GamedataBytes.Compress(content),
                    Size = content.Length,
                };

                dbCtx.GamedataBuilds.Add(row);

                try
                {
                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                catch (DbUpdateException ex)
                {
                    // Another silo kept the same build a moment before; it is the same content.
                    logger.LogDebug(
                        ex,
                        "Gamedata build {File} {Hash} was kept by another silo first",
                        file,
                        hash
                    );
                }

                await PruneAsync(dbCtx, file, hash, ct).ConfigureAwait(false);

                logger.LogInformation(
                    "Built {File} {Hash}: {Count} entries, {Size} bytes",
                    file,
                    hash,
                    count,
                    content.Length
                );
            }

            var result = new GamedataFileContent(row.ToSnapshot(), row.Content);

            _current[file] = new Build(version, inputs, stamp, result);

            return result;
        }
        finally
        {
            _building.Release();
        }
    }

    private static async Task<(byte[] Content, int Count)> FurnitureDataAsync(
        TurboDbContext dbCtx,
        FurnitureOfferCatalog.Stamps stamps,
        CancellationToken ct
    )
    {
        var definitions = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
            .OrderBy(x => x.SpriteId)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (
            FurnitureDataWriter.Write(definitions, stamps.Offers, stamps.BuildersClubOffers),
            definitions.Count
        );
    }

    private static async Task<(byte[] Content, int Count)> ProductDataAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var products = await dbCtx
            .GamedataProducts.AsNoTracking()
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.Description,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (
            ProductDataFile.Write(products.Select(x => (x.Code, x.Name, x.Description))),
            products.Count
        );
    }

    private static async Task<(byte[] Content, int Count)> FigureDataAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var rows = await dbCtx
            .GamedataFigures.AsNoTracking()
            .Select(x => new { x.Kind, x.Data })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var records = new List<(FigureRecordKind, JsonObject)>();

        foreach (var row in rows)
        {
            try
            {
                records.Add(
                    (row.Kind, FigureRecords.Normalize(row.Kind, FigureRecords.Parse(row.Data)))
                );
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                // A record that doesn't read would fail the client too: left out.
            }
        }

        return (FigureDataFile.Write(records), records.Count(x => x.Item1 == FigureRecordKind.Set));
    }

    private static async Task<(byte[] Content, int Count)> BadgePartsAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var parts = await dbCtx
            .GuildBadgeParts.AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var colors = await dbCtx.GuildColors.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

        return (BadgePartsFile.Write(parts, colors), parts.Count);
    }

    /// <summary>
    /// FigureMap.json from every clothing bundle with a file: its parts as Habbo's figure map lists
    /// them, or - for one no map has listed yet - as its file holds them.
    /// </summary>
    private async Task<(byte[] Content, int Count)> FigureMapAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var rows = await BundleRowsAsync(dbCtx, AssetBundleKind.Figure, ct).ConfigureAwait(false);
        var libraries = new List<(string, string?, string)>();

        foreach (var row in rows)
            libraries.Add(
                (
                    row.Name,
                    row.Revision,
                    row.MapEntries
                        ?? await FilePartsAsync(row.Name, row.Hash!, ct).ConfigureAwait(false)
                )
            );

        return (ClientMaps.WriteFigureMap(libraries), libraries.Count);
    }

    /// <summary>
    /// EffectMap.json from every effect bundle with a file: its effects as Habbo's effect map lists
    /// them, or - for one no map has listed - each id that loads it, worn as an effect.
    /// </summary>
    private static async Task<(byte[] Content, int Count)> EffectMapAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var rows = await BundleRowsAsync(dbCtx, AssetBundleKind.Effect, ct).ConfigureAwait(false);
        var libraries = rows.Select(x =>
                (
                    x.Name,
                    x.Revision,
                    x.MapEntries ?? ClientMaps.EffectEntriesOf(AssetBundleIds.Parse(x.Ids))
                )
            )
            .ToList();

        return (
            ClientMaps.WriteEffectMap(libraries),
            libraries.Sum(x => ClientMaps.Count(x.Item3))
        );
    }

    private static Task<List<BundleRow>> BundleRowsAsync(
        TurboDbContext dbCtx,
        AssetBundleKind kind,
        CancellationToken ct
    ) =>
        dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == kind && x.Hash != null)
            .OrderBy(x => x.Name)
            .Select(x => new BundleRow(x.Name, x.Revision, x.Hash, x.Ids, x.MapEntries))
            .ToListAsync(ct);

    private sealed record BundleRow(
        string Name,
        string? Revision,
        string? Hash,
        string? Ids,
        string? MapEntries
    );

    /// <summary>A clothing bundle's parts read from its file, once per hash; none when it does not read.</summary>
    private async Task<string> FilePartsAsync(string name, string hash, CancellationToken ct)
    {
        if (_fileParts.TryGetValue((name, hash), out var known))
            return known;

        var parts = "[]";

        try
        {
            var path = store.FullPathOf(AssetBundleKind.Figure, name);

            if (File.Exists(path))
                parts = ClientMaps.FigureEntriesOf(
                    NitroBundle.Read(await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false)),
                    name
                );
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            logger.LogWarning(
                ex,
                "The clothing bundle {Name} gives no parts: it does not read",
                name
            );
        }

        _fileParts[(name, hash)] = parts;

        return parts;
    }

    private static async Task<(byte[] Content, int Count)> ExternalTextsAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var texts = await dbCtx
            .GamedataTexts.AsNoTracking()
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (ExternalTextsFile.Write(texts.Select(x => (x.Key, x.Value))), texts.Count);
    }

    /// <summary>
    /// The hotel's variables, each that follows a setting or a file written with what it follows
    /// (<see cref="ExternalVariablesFile.Resolve"/>).
    /// </summary>
    private async Task<(byte[] Content, int Count)> ExternalVariablesAsync(
        TurboDbContext dbCtx,
        ExternalVariablesFile.Addresses addresses,
        CancellationToken ct
    )
    {
        var rows = await dbCtx
            .GamedataVariables.AsNoTracking()
            .Select(x => new
            {
                x.Key,
                x.Value,
                x.SettingPath,
                x.LinkedFile,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (
            ExternalVariablesFile.Write(
                rows.Select(x =>
                    (
                        x.Key,
                        ExternalVariablesFile.Resolve(
                            x.Value,
                            x.SettingPath,
                            x.LinkedFile,
                            addresses,
                            settings
                        )
                    )
                )
            ),
            rows.Count
        );
    }

    /// <summary>Removes the builds older than the ones kept, never the current one.</summary>
    private async Task PruneAsync(
        TurboDbContext dbCtx,
        string file,
        string currentHash,
        CancellationToken ct
    )
    {
        try
        {
            var old = await dbCtx
                .GamedataBuilds.Where(x => x.File == file && x.Hash != currentHash)
                .OrderByDescending(x => x.Id)
                .Skip(_config.KeepBuilds)
                .Select(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (old.Count > 0)
                await dbCtx
                    .GamedataBuilds.Where(x => old.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The new build is kept; old ones go next time.
            logger.LogError(ex, "Removing old {File} builds failed", file);
        }
    }

    private static void RequireKnown(string file)
    {
        if (!GamedataFiles.IsKnown(file))
            throw new ArgumentException(
                $"{file} is not a gamedata file the hotel builds.",
                nameof(file)
            );
    }

    private sealed record Build(
        int Version,
        object? Inputs,
        object? Stamp,
        GamedataFileContent Content
    )
    {
        public bool IsFor(int version, object? inputs, object? stamp) =>
            Version == version && Equals(Inputs, inputs) && Equals(Stamp, stamp);
    }
}
