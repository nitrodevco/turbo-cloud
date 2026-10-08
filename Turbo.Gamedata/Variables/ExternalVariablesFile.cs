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

namespace Turbo.Gamedata.Variables;

/// <summary>
/// The client's external variables as the file Nitro loads: one JSON object, a key for each
/// variable, in ordinal order. A value is kept as JSON text, written back as it is.
/// <para>
/// The hotel writes the address of each of its own gamedata files itself, by hash, as Habbo's
/// external variables do (<see cref="STAMPED"/>): the client then loads exactly the build the
/// variables were built with, cached for good, with no redirect on the way.
/// </para>
/// </summary>
internal static class ExternalVariablesFile
{
    /// <summary>The variable each of the hotel's gamedata files is named by, as Nitro reads it.</summary>
    public static readonly IReadOnlyDictionary<string, string> STAMPED = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        [GamedataFiles.FURNITURE_DATA] = "furnituredata.url",
        [GamedataFiles.PRODUCT_DATA] = "productdata.url",
        [GamedataFiles.EXTERNAL_TEXTS] = "gamedata.urls.externalTexts",
        [GamedataFiles.FIGURE_DATA] = "figuredata.url",
    };

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

    public static bool IsStamped(string key) =>
        STAMPED.Values.Contains(key, StringComparer.Ordinal);

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
    /// The variables the hotel writes itself, each its file's address by the current hash, as a
    /// JSON string. Empty without <paramref name="publicUrl"/>: a build is the same on every
    /// silo, so the address can't come from a request.
    /// </summary>
    public static async Task<Stamps> StampsAsync(
        IGamedataFileService files,
        string publicUrl,
        CancellationToken ct
    )
    {
        var stamps = new List<(string Key, string Value)>();

        if (string.IsNullOrWhiteSpace(publicUrl))
            return new Stamps(stamps);

        var root = publicUrl.Trim().TrimEnd('/');

        foreach (var (file, key) in STAMPED)
        {
            var current = await files.GetCurrentAsync(file, ct).ConfigureAwait(false);

            stamps.Add(
                (
                    key,
                    JsonValue
                        .Create($"{root}/gamedata/{file}/{current.File.Hash}")
                        .ToJsonString(TEXT)
                )
            );
        }

        return new Stamps(stamps);
    }

    /// <summary>
    /// The variables the hotel writes itself; two are equal when they write the same, so the file
    /// is built again only when one of the addresses has moved.
    /// </summary>
    public sealed record Stamps(IReadOnlyList<(string Key, string Value)> Entries)
    {
        public bool Equals(Stamps? other) =>
            other is not null && Entries.SequenceEqual(other.Entries);

        public override int GetHashCode() => Entries.Count;
    }
}
