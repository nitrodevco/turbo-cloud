using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Assets;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata.HandItems;

/// <summary>Materializes carry parameters and their art exclusively from the local asset store.</summary>
internal sealed class HandItemCatalog(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    IHotelTextProvider texts,
    ILogger<HandItemCatalog> logger
) : IHandItemCatalog
{
    private const string ACTION_LIBRARY = "habbo-avatar-render-lib";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _signature;
    private ImmutableArray<HandItemSnapshot> _items = [];
    private Dictionary<int, byte[]> _previews = [];
    private string? _message;

    public async Task<HandItemCatalogSnapshot> GetAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RefreshAsync(ct).ConfigureAwait(false);
            var labels = await texts.GetTextsByPrefixAsync("handitem", ct).ConfigureAwait(false);
            var items = _items
                .Select(item =>
                    item with
                    {
                        Name = labels.TryGetText(
                            "handitem" + item.Id.ToString(CultureInfo.InvariantCulture),
                            out var name
                        )
                            ? name
                            : null,
                    }
                )
                .ToImmutableArray();
            return new HandItemCatalogSnapshot
            {
                Items = items,
                Revision = Hash((_signature ?? "") + JsonSerializer.Serialize(items)),
                Message = _message,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> GetPreviewAsync(int id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RefreshAsync(ct).ConfigureAwait(false);
            return _previews.TryGetValue(id, out var bytes) ? bytes.ToArray() : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            var rows = await db
                .AssetBundles.AsNoTracking()
                .Where(x =>
                    (
                        x.Kind == AssetBundleKind.Figure
                        && x.MapEntries != null
                        && x.MapEntries.Contains("\"ri\"")
                    ) || (x.Kind == AssetBundleKind.Template && x.Name == ACTION_LIBRARY)
                )
                .OrderBy(x => x.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var actions = rows.FirstOrDefault(x => x.Kind == AssetBundleKind.Template);
            var sources = rows;
            var signature = Signature(sources);
            if (signature == _signature)
                return;
            var figures = rows.Where(x => x.Kind == AssetBundleKind.Figure)
                .Select(x => (Row: x, Parts: Parts(x)))
                .Where(x => x.Parts.Count > 0)
                .ToList();
            if (actions is null || !Ready(actions))
            {
                Publish(
                    signature,
                    [],
                    [],
                    "Local carry actions are unavailable. Sync the avatar template assets."
                );
                return;
            }
            var bundle = await ReadAsync(actions, ct).ConfigureAwait(false);
            if (!bundle.Files.TryGetValue("actions.json", out var actionBytes))
                throw new AssetFormatException("The local avatar template has no actions.json.");
            var root = JsonNode.Parse(actionBytes) as JsonObject;
            var carry = (root?["actions"] as JsonArray)
                ?.OfType<JsonObject>()
                .FirstOrDefault(x => Text(x["id"]) == "CarryItem");
            if (
                carry?["params"] is not JsonArray parameters
                || Text(carry["assetPartDefinition"]) is not { Length: > 0 } definition
            )
                throw new AssetFormatException(
                    "The local actions contain no readable CarryItem parameters."
                );
            var mappings = new SortedDictionary<int, int>();
            foreach (var parameter in parameters.OfType<JsonObject>())
                if (Positive(parameter["id"]) is { } id && Positive(parameter["value"]) is { } part)
                    mappings.TryAdd(id, part);
            var art = new Dictionary<int, (string Library, bool Available, byte[]? Preview)>();
            long previewBytes = 0;
            foreach (var (row, parts) in figures)
            {
                var wanted = mappings.Values.Where(parts.Contains).Distinct().ToArray();
                if (wanted.Length == 0)
                    continue;
                HandItemPreview? reader = null;
                if (Ready(row))
                {
                    try
                    {
                        reader = new HandItemPreview(
                            await ReadAsync(row, ct).ConfigureAwait(false),
                            row.Name
                        );
                    }
                    catch (Exception ex) when (SourceError(ex))
                    {
                        logger.LogWarning(
                            ex,
                            "Cannot read local hand-item figure library {Library}",
                            row.Name
                        );
                        // Keep the previous coherent materialization while a file is being replaced.
                        if (_signature is not null)
                            throw;
                    }
                }
                foreach (var part in wanted)
                {
                    var preview = reader?.Draw(definition, part);
                    if (preview is not null)
                    {
                        previewBytes += preview.Length;
                        AssetLimits.CheckInflatedSize(previewBytes, "Hand-item previews");
                    }
                    var candidate = (row.Name, reader?.Contains(definition, part) == true, preview);
                    if (
                        !art.TryGetValue(part, out var previous)
                        || (!previous.Available && candidate.Item2)
                    )
                        art[part] = candidate;
                }
            }
            var previews = new Dictionary<int, byte[]>();
            var items = mappings
                .Select(pair =>
                {
                    art.TryGetValue(pair.Value, out var found);
                    if (found.Preview is not null)
                        previews[pair.Key] = found.Preview;
                    return new HandItemSnapshot
                    {
                        Id = pair.Key,
                        AssetPartId = pair.Value,
                        Library = found.Library,
                        AssetAvailable = found.Available,
                        PreviewAvailable = found.Preview is not null,
                    };
                })
                .ToImmutableArray();
            ct.ThrowIfCancellationRequested();
            if (Signature(sources) != signature)
                throw new IOException("Local hand-item sources changed while being read.");
            Publish(signature, items, previews, null);
        }
        catch (Exception ex) when (SourceError(ex))
        {
            logger.LogWarning(ex, "Cannot materialize the local hand-item catalogue");
            _message =
                "Local hand-item assets could not be read. "
                + (
                    _signature is null
                        ? "Sync the avatar template and figure assets."
                        : "Showing the last readable catalogue; retry after asset sync completes."
                );
        }
    }

    private void Publish(
        string signature,
        ImmutableArray<HandItemSnapshot> items,
        Dictionary<int, byte[]> previews,
        string? message
    )
    {
        _signature = signature;
        _items = items;
        _previews = previews;
        _message = message;
    }

    private bool Ready(AssetBundleEntity row) =>
        row.Hash is not null
        && row.Error is null
        && store.IsValidName(row.Name)
        && File.Exists(store.FullPathOf(row.Kind, row.Name));

    private string Signature(List<AssetBundleEntity> rows) =>
        Hash(
            string.Join(
                "\n",
                rows.Select(row =>
                {
                    var file = store.IsValidName(row.Name)
                        ? new FileInfo(store.FullPathOf(row.Kind, row.Name))
                        : null;
                    return JsonSerializer.Serialize(
                        new
                        {
                            row.Kind,
                            row.Name,
                            row.Hash,
                            row.Error,
                            row.MapEntries,
                            row.Revision,
                            row.UpdatedAt,
                            Exists = file?.Exists,
                            Length = file?.Exists == true ? file.Length : 0,
                            Modified = file?.Exists == true ? file.LastWriteTimeUtc.Ticks : 0,
                        }
                    );
                })
            )
        );

    private async Task<NitroBundle> ReadAsync(AssetBundleEntity row, CancellationToken ct)
    {
        var path = store.FullPathOf(row.Kind, row.Name);
        AssetLimits.CheckInflatedSize(new FileInfo(path).Length, "Local bundle");
        return NitroBundle.Read(await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false));
    }

    private HashSet<int> Parts(AssetBundleEntity row)
    {
        try
        {
            return row.MapEntries is null
                ? []
                : (JsonNode.Parse(row.MapEntries) as JsonArray)
                    ?.OfType<JsonObject>()
                    .Where(x => Text(x["type"]) == "ri")
                    .Select(x => Positive(x["id"]))
                    .OfType<int>()
                    .ToHashSet()
                    ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid figure map entries for {Library}", row.Name);
            return [];
        }
    }

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    internal static int? Positive(JsonNode? node) =>
        int.TryParse(node?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0
            ? id
            : null;

    private static bool SourceError(Exception ex) =>
        ex
            is IOException
                or UnauthorizedAccessException
                or AssetFormatException
                or JsonException
                or InvalidOperationException
                or ArgumentException;
}
