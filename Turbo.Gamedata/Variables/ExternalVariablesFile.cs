using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Settings;

namespace Turbo.Gamedata.Variables;

/// <summary>
/// The client's external variables as the file Nitro loads: one JSON object, a key for each
/// variable, in ordinal order. A value is kept as JSON text, written back as it is.
/// <para>
/// A variable may follow something instead of holding a value of its own (<see cref="Resolve"/>):
/// a server setting, or the address of one of the hotel's gamedata files by hash, as Habbo's
/// external variables give theirs. Following a file, the client loads exactly the build the
/// variables were built with, cached for good, with no redirect on the way.
/// </para>
/// </summary>
internal static class ExternalVariablesFile
{
    /// <summary>The files a variable may follow the address of: every one the hotel builds but this.</summary>
    public static readonly IReadOnlyList<string> LINKABLE =
    [
        GamedataFiles.FURNITURE_DATA,
        GamedataFiles.PRODUCT_DATA,
        GamedataFiles.EXTERNAL_TEXTS,
        GamedataFiles.FIGURE_DATA,
    ];

    // The file is served as JSON, never into a page, so text is written as it is: an apostrophe
    // stays an apostrophe for staff reading the panel.
    private static readonly JsonSerializerOptions TEXT = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions WRITER = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    public static bool IsLinkable(string file) => LINKABLE.Contains(file, StringComparer.Ordinal);

    /// <summary>
    /// The value the file writes for a variable: its setting's value, or its file's address by
    /// hash, when it follows one that can be written; otherwise its own.
    /// </summary>
    public static string Resolve(
        string value,
        string? setting,
        string? file,
        Addresses addresses,
        IServerSettings settings
    ) =>
        setting is not null ? settings.GetValue(setting) ?? value
        : file is not null ? addresses.Of(file) ?? value
        : value;

    /// <summary>
    /// A file's address that never changes, <c>/gamedata/&lt;file&gt;/0</c> (it redirects to the
    /// current build), under the public address when there is one: what a variable that stops
    /// following the file keeps.
    /// </summary>
    public static string StableAddress(string publicUrl, string file) =>
        Text($"{Root(publicUrl)}/gamedata/{file}/0");

    /// <summary>
    /// A value as it is kept: JSON, written compactly. Throws <see cref="ArgumentException"/>
    /// when it isn't JSON.
    /// </summary>
    public static string Normalize(string value)
    {
        try
        {
            var node = JsonNode.Parse(value);

            return node is null ? "null" : node.ToJsonString(TEXT);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"A value is JSON: \"text\" in quotes, true, 120 or [1, 2]. {ex.Message}",
                nameof(value),
                ex
            );
        }
    }

    /// <summary>
    /// A client config's variables in its own order, each value kept as JSON; a key given twice
    /// is listed twice. Throws <see cref="ArgumentException"/> when the config isn't a JSON object.
    /// </summary>
    public static List<(string Key, string Value)> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }
            );

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "The config is one JSON object: { \"key\": value, ... }.",
                    nameof(json)
                );

            return
            [
                .. document
                    .RootElement.EnumerateObject()
                    .Select(x => (x.Name, Normalize(x.Value.GetRawText()))),
            ];
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The config isn't JSON. {ex.Message}", nameof(json), ex);
        }
    }

    /// <summary>The variables as the file, by key in ordinal order, UTF-8.</summary>
    public static byte[] Write(IEnumerable<(string Key, string Value)> variables)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER))
        {
            writer.WriteStartObject();

            foreach (var (key, value) in variables.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                writer.WriteRawValue(value);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// The addresses of the files a variable may follow, by their current hash. None without
    /// <paramref name="publicUrl"/>: a build is the same on every silo, so the address can't come
    /// from a request, and a variable following a file then writes its own value.
    /// </summary>
    public static async Task<Addresses> AddressesAsync(
        IGamedataFileService files,
        string publicUrl,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            return Addresses.NONE;

        var hashes = new List<(string File, string Hash)>();

        foreach (var file in LINKABLE)
        {
            var current = await files.GetCurrentAsync(file, ct).ConfigureAwait(false);

            hashes.Add((file, current.File.Hash));
        }

        return new Addresses(Root(publicUrl), hashes);
    }

    private static string Root(string publicUrl) => publicUrl.Trim().TrimEnd('/');

    private static string Text(string text) => JsonValue.Create(text).ToJsonString(TEXT);

    /// <summary>
    /// What the file is made from besides its rows: the files' addresses, and the version of the
    /// server's settings the linked variables were read at. Two are equal when they write the
    /// same, so the file is built again only when an address has moved or a setting changed.
    /// </summary>
    public sealed record Inputs(Addresses Addresses, int SettingsVersion);

    /// <summary>
    /// The files' addresses by hash under <see cref="Root"/>; two are equal when they give the same
    /// addresses.
    /// </summary>
    public sealed record Addresses(string? Root, IReadOnlyList<(string File, string Hash)> Hashes)
    {
        public static readonly Addresses NONE = new(null, []);

        /// <summary>The file's address by its current hash, as JSON; null without a public address.</summary>
        public string? Of(string file) =>
            Root is null ? null
            : Hashes.FirstOrDefault(x => x.File == file) is { File: not null } at
                ? Text($"{Root}/gamedata/{file}/{at.Hash}")
            : null;

        public bool Equals(Addresses? other) =>
            other is not null && Root == other.Root && Hashes.SequenceEqual(other.Hashes);

        public override int GetHashCode() => HashCode.Combine(Root, Hashes.Count);
    }
}
