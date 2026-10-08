using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Products;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Files;

/// <summary>
/// The gamedata files built from the database (<see cref="IGamedataFileService"/>): FurnitureData
/// from the definitions and the catalogs, the external texts from <c>gamedata_texts</c>, the
/// external variables from <c>gamedata_variables</c> with the other files' addresses by hash.
/// Each file's current build is held in memory and built again when what it is made from
/// changes - a catalog snapshot replaced (a catalog published), another file's hash (for the
/// variables), or its rows said to have changed (<see cref="Invalidate"/>). Each build is kept in <c>gamedata_builds</c> by its hash, the
/// newest <see cref="GamedataConfig.KeepBuilds"/> of a file besides the current one, so a client
/// that loaded an address before a rebuild still finds it. The same content always hashes the
/// same, so every silo names a build alike.
/// </summary>
internal sealed class GamedataFileService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    FurnitureOfferCatalog offers,
    ILogger<GamedataFileService> logger
) : IGamedataFileService
{
    private readonly GamedataConfig _config = config.Value;
    private readonly SemaphoreSlim _building = new(1, 1);
    private readonly ConcurrentDictionary<string, Build> _current = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _versions = new(StringComparer.Ordinal);

    public async Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct)
    {
        RequireKnown(file);

        var inputs = await InputsAsync(file, ct).ConfigureAwait(false);

        if (_current.TryGetValue(file, out var current) && current.IsFor(VersionOf(file), inputs))
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

    public void Invalidate(string file) =>
        _versions.AddOrUpdate(file, 1, (_, version) => version + 1);

    private int VersionOf(string file) =>
        _versions.TryGetValue(file, out var version) ? version : 0;

    /// <summary>
    /// What a file is made from besides its rows: the catalogs, for FurnitureData; the other
    /// files' addresses, for the external variables. Worked out outside the build lock, as the
    /// addresses may build their files.
    /// </summary>
    private async Task<object?> InputsAsync(string file, CancellationToken ct) =>
        file switch
        {
            GamedataFiles.FURNITURE_DATA => await offers.GetAsync(ct).ConfigureAwait(false),
            GamedataFiles.EXTERNAL_VARIABLES => await ExternalVariablesFile
                .StampsAsync(this, _config.PublicUrl, ct)
                .ConfigureAwait(false),
            _ => null,
        };

    private async Task<GamedataFileContent> BuildAsync(
        string file,
        bool force,
        CancellationToken ct
    )
    {
        var inputs = await InputsAsync(file, ct).ConfigureAwait(false);

        await _building.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // Whoever waited behind a build that has just finished takes that build.
            var version = VersionOf(file);

            if (!force && _current.TryGetValue(file, out var built) && built.IsFor(version, inputs))
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
                GamedataFiles.EXTERNAL_VARIABLES => await ExternalVariablesAsync(
                        dbCtx,
                        (ExternalVariablesFile.Stamps)inputs!,
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

            _current[file] = new Build(version, inputs, result);

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

    /// <summary>The hotel's variables, with its own addresses written over any of the same key.</summary>
    private static async Task<(byte[] Content, int Count)> ExternalVariablesAsync(
        TurboDbContext dbCtx,
        ExternalVariablesFile.Stamps stamps,
        CancellationToken ct
    )
    {
        var rows = await dbCtx
            .GamedataVariables.AsNoTracking()
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var variables = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        foreach (var (key, value) in stamps.Entries)
            variables[key] = value;

        return (
            ExternalVariablesFile.Write(variables.Select(x => (x.Key, x.Value))),
            variables.Count
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

    private sealed record Build(int Version, object? Inputs, GamedataFileContent Content)
    {
        public bool IsFor(int version, object? inputs) =>
            Version == version && Equals(Inputs, inputs);
    }
}
