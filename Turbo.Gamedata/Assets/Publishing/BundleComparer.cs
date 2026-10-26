using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets;
using Turbo.Assets.Nitro;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// What changed from the copy of a file a publish target holds to the hotel's. A bundle is read file
/// by file (a zip's entries): its window templates (<c>templates.json</c>) by template, its texts
/// (<c>DefaultLocalizations_&lt;language&gt;.json</c>) by key, and every other file (the images, the
/// asset data) by its bytes. The client build stamp a JSON file is packed with
/// (<c>"version":"99_classic-js-69df354f4175"</c>) is set aside: a bundle that differs only in it
/// changes nothing for players.
/// </summary>
internal static partial class BundleComparer
{
    /// <summary>The other files a comparison lists; it counts them all.</summary>
    public const int MAX_ENTRIES = 300;

    /// <summary>The text keys a language's difference lists; it counts them all.</summary>
    public const int MAX_TEXT_EDITS = 200;

    private const string TEXTS_PREFIX = "DefaultLocalizations_";
    private const string TEXTS_SUFFIX = ".json";
    private const string STAMP_PROPERTY = "version";

    private static readonly byte[] PNG = [0x89, (byte)'P', (byte)'N', (byte)'G'];

    [GeneratedRegex(@"^\d+_classic-js-[0-9a-f]+$")]
    private static partial Regex Stamp();

    [GeneratedRegex(@"\$\{([^${}]+)\}")]
    private static partial Regex TextKey();

    /// <summary>What a comparison found, for <see cref="AssetPublishComparison"/>.</summary>
    public sealed record Found(
        AssetCompareResult Result,
        string? StampBefore = null,
        string? StampAfter = null,
        ImmutableArray<AssetWindowDiff> Windows = default,
        ImmutableArray<AssetTextDiff> Texts = default,
        ImmutableArray<AssetEntryDiff> Entries = default,
        int EntriesChanged = 0,
        string? Problem = null
    );

    /// <summary>
    /// Compares the target's copy (<paramref name="before"/>) with the hotel's; <paramref name="name"/>
    /// is the file's name, which says, when it is not a <paramref name="bundle"/>, whether it is an image.
    /// </summary>
    public static Found Compare(string name, byte[] before, byte[] after, bool bundle)
    {
        if (!bundle)
            return before.AsSpan().SequenceEqual(after)
                ? new Found(AssetCompareResult.Same)
                : new Found(
                    AssetCompareResult.Changed,
                    Entries: [EntryOf(name, before, after)],
                    EntriesChanged: 1
                );

        IReadOnlyDictionary<string, byte[]> old;
        IReadOnlyDictionary<string, byte[]> now;

        try
        {
            old = NitroBundle.Read(before).Files;
        }
        catch (AssetFormatException ex)
        {
            return new Found(
                AssetCompareResult.Unreadable,
                Problem: $"The target's copy is not a bundle it can read: {ex.Message}"
            );
        }

        try
        {
            now = NitroBundle.Read(after).Files;
        }
        catch (AssetFormatException ex)
        {
            return new Found(
                AssetCompareResult.Unreadable,
                Problem: $"The hotel's copy is not a bundle it can read: {ex.Message}"
            );
        }

        string? stampBefore = null;
        string? stampAfter = null;
        var windows = new List<AssetWindowDiff>();
        var texts = new List<AssetTextDiff>();
        var entries = new List<AssetEntryDiff>();

        foreach (var file in old.Keys.Union(now.Keys).Order(StringComparer.Ordinal))
        {
            var was = old.GetValueOrDefault(file);
            var @is = now.GetValueOrDefault(file);
            var same = was is not null && @is is not null && was.AsSpan().SequenceEqual(@is);

            // The templates carry the stamp, so they are read even when they are the same.
            if (same && file != ClientTemplates.TEMPLATES_FILE)
                continue;

            var json = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var oldJson = json ? Unstamped(was, ref stampBefore) : null;
            var newJson = json ? Unstamped(@is, ref stampAfter) : null;

            if (same)
                continue;

            if (
                json
                && (was is null || oldJson is not null)
                && (@is is null || newJson is not null)
            )
            {
                if (JsonNode.DeepEquals(oldJson, newJson))
                    continue;

                if (file == ClientTemplates.TEMPLATES_FILE)
                {
                    windows.AddRange(WindowsOf(oldJson as JsonObject, newJson as JsonObject));

                    continue;
                }

                if (LanguageOf(file) is { } language)
                {
                    if (TextsOf(language, oldJson as JsonObject, newJson as JsonObject) is { } diff)
                        texts.Add(diff);

                    continue;
                }
            }

            entries.Add(EntryOf(file, was, @is));
        }

        var result =
            windows.Count > 0 || texts.Count > 0 || entries.Count > 0 ? AssetCompareResult.Changed
            : stampBefore != stampAfter ? AssetCompareResult.StampOnly
            : AssetCompareResult.Same;

        return new Found(
            result,
            stampBefore,
            stampAfter,
            [.. windows],
            [.. texts],
            [.. entries.Take(MAX_ENTRIES)],
            entries.Count
        );
    }

    /// <summary>Whether a file is one a comparison can show: a PNG or a GIF.</summary>
    public static bool IsImage(string name) =>
        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The JSON file without the build stamp it was packed with, which is kept in
    /// <paramref name="stamp"/> (the first one found); null when it is missing or not JSON.
    /// </summary>
    private static JsonNode? Unstamped(byte[]? data, ref string? stamp)
    {
        if (data is null)
            return null;

        JsonNode? node;

        try
        {
            node = JsonNode.Parse(data);
        }
        catch (JsonException)
        {
            return null;
        }

        if (
            node is JsonObject root
            && root[STAMP_PROPERTY] is JsonValue value
            && value.TryGetValue<string>(out var text)
            && Stamp().IsMatch(text)
        )
        {
            root.Remove(STAMP_PROPERTY);
            stamp ??= text;
        }

        return node;
    }

    private static string? LanguageOf(string file) =>
        file.StartsWith(TEXTS_PREFIX, StringComparison.Ordinal)
        && file.EndsWith(TEXTS_SUFFIX, StringComparison.Ordinal)
        && file.Length > TEXTS_PREFIX.Length + TEXTS_SUFFIX.Length
            ? file[TEXTS_PREFIX.Length..^TEXTS_SUFFIX.Length]
            : null;

    private static IEnumerable<AssetWindowDiff> WindowsOf(JsonObject? before, JsonObject? after)
    {
        var old = before?["templates"] as JsonObject;
        var now = after?["templates"] as JsonObject;
        var ids = (old?.Select(x => x.Key) ?? [])
            .Union(now?.Select(x => x.Key) ?? [])
            .Order(StringComparer.Ordinal);

        foreach (var id in ids)
        {
            var was = old?[id];
            var @is = now?[id];

            if (was is null)
                yield return new AssetWindowDiff(id, AssetDiffChange.Added, [], [], [], [], []);
            else if (@is is null)
                yield return new AssetWindowDiff(id, AssetDiffChange.Removed, [], [], [], [], []);
            else if (!JsonNode.DeepEquals(was, @is))
            {
                var oldElements = ElementsOf(was);
                var newElements = ElementsOf(@is);
                var oldKeys = TextKeysOf(was);
                var newKeys = TextKeysOf(@is);

                yield return new AssetWindowDiff(
                    id,
                    AssetDiffChange.Changed,
                    [.. newElements.Keys.Except(oldElements.Keys).Order(StringComparer.Ordinal)],
                    [.. oldElements.Keys.Except(newElements.Keys).Order(StringComparer.Ordinal)],
                    [
                        .. oldElements
                            .Keys.Intersect(newElements.Keys)
                            .Where(x => !oldElements[x].SequenceEqual(newElements[x]))
                            .Order(StringComparer.Ordinal),
                    ],
                    [.. newKeys.Except(oldKeys).Order(StringComparer.Ordinal)],
                    [.. oldKeys.Except(newKeys).Order(StringComparer.Ordinal)]
                );
            }
        }
    }

    /// <summary>A template's named elements, at any depth: each as its JSON without its children, by name.</summary>
    private static Dictionary<string, List<string>> ElementsOf(JsonNode template)
    {
        var named = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var stack = new Stack<JsonNode?>(template["elements"] as JsonArray ?? []);

        while (stack.TryPop(out var node))
        {
            if (node is not JsonObject element)
                continue;

            foreach (var child in element["children"] as JsonArray ?? [])
                stack.Push(child);

            if (element["name"] is not JsonValue value || !value.TryGetValue<string>(out var name))
                continue;

            var own = (JsonObject)element.DeepClone();

            own.Remove("children");

            if (!named.TryGetValue(name, out var list))
                named[name] = list = [];

            list.Add(own.ToJsonString());
        }

        foreach (var list in named.Values)
            list.Sort(StringComparer.Ordinal);

        return named;
    }

    /// <summary>The text keys a template's strings name (<c>${catalog.title}</c> names <c>catalog.title</c>).</summary>
    private static HashSet<string> TextKeysOf(JsonNode template)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<JsonNode?>([template]);

        while (stack.TryPop(out var node))
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (_, child) in obj)
                        stack.Push(child);

                    break;
                case JsonArray array:
                    foreach (var child in array)
                        stack.Push(child);

                    break;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    foreach (Match match in TextKey().Matches(text))
                        keys.Add(match.Groups[1].Value);

                    break;
            }
        }

        return keys;
    }

    private static AssetTextDiff? TextsOf(string language, JsonObject? before, JsonObject? after)
    {
        var old = before ?? [];
        var now = after ?? [];
        var edits = new List<AssetTextEdit>();

        foreach (var key in old.Select(x => x.Key).Union(now.Select(x => x.Key)))
        {
            var was = TextOf(old[key]);
            var @is = TextOf(now[key]);

            if (!old.ContainsKey(key))
                edits.Add(new AssetTextEdit(key, AssetDiffChange.Added, null, @is));
            else if (!now.ContainsKey(key))
                edits.Add(new AssetTextEdit(key, AssetDiffChange.Removed, was, null));
            else if (was != @is)
                edits.Add(new AssetTextEdit(key, AssetDiffChange.Changed, was, @is));
        }

        if (edits.Count == 0)
            return null;

        return new AssetTextDiff(
            language,
            edits.Count(x => x.Change == AssetDiffChange.Added),
            edits.Count(x => x.Change == AssetDiffChange.Removed),
            edits.Count(x => x.Change == AssetDiffChange.Changed),
            [
                .. edits
                    .OrderBy(x => x.Change)
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .Take(MAX_TEXT_EDITS),
            ]
        );
    }

    private static string? TextOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : node?.ToJsonString();

    private static AssetEntryDiff EntryOf(string name, byte[]? before, byte[]? after) =>
        new(
            name,
            before is null ? AssetDiffChange.Added
                : after is null ? AssetDiffChange.Removed
                : AssetDiffChange.Changed,
            before?.LongLength,
            after?.LongLength,
            before is null ? null : PixelsOf(before),
            after is null ? null : PixelsOf(after)
        );

    /// <summary>A PNG's or a GIF's width and height, from its header; null for anything else.</summary>
    private static AssetPixelSize? PixelsOf(byte[] data)
    {
        if (data.Length >= 24 && data.AsSpan(0, 4).SequenceEqual(PNG))
            return new AssetPixelSize(
                BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)),
                BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20))
            );

        if (data.Length >= 10 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
            return new AssetPixelSize(
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)),
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8))
            );

        return null;
    }
}
