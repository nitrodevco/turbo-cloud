using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Assets.Nitro;

/// <summary>
/// A <c>.nitro</c> bundle: a zip of <c>&lt;name&gt;.json</c> (the asset data the client reads),
/// <c>&lt;name&gt;.png</c> (the packed sheet) and <c>&lt;name&gt;_spritesheet.json</c> (its frames),
/// as nitro-renderer's <c>NitroBundle</c> reads one.
/// </summary>
public sealed class NitroBundle
{
    private const string SPRITESHEET_SUFFIX = "_spritesheet.json";

    private NitroBundle(IReadOnlyDictionary<string, byte[]> files)
    {
        Files = files;
    }

    public IReadOnlyDictionary<string, byte[]> Files { get; }

    /// <summary>
    /// The date every written entry carries, so the same files always zip to the same bytes - and
    /// the same hash, by which a bundle is known.
    /// </summary>
    private static readonly DateTimeOffset ENTRY_DATE = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static NitroBundle Create(IReadOnlyDictionary<string, byte[]> files) => new(files);

    /// <summary>
    /// The bundle as a zip, its files in the order given. A PNG is stored as it is - it is
    /// compressed already - and the rest deflated.
    /// </summary>
    public byte[] Write()
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in Files)
            {
                var entry = zip.CreateEntry(
                    name,
                    name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? CompressionLevel.NoCompression
                        : CompressionLevel.Optimal
                );

                entry.LastWriteTime = ENTRY_DATE;

                using var stream = entry.Open();

                stream.Write(data);
            }
        }

        return output.ToArray();
    }

    public static bool IsNitro(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[0] == (byte)'P' && data[1] == (byte)'K';

    public static NitroBundle Read(byte[] data)
    {
        if (!IsNitro(data))
            throw new AssetFormatException("Not a .nitro bundle: the file is not a zip.");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;

        try
        {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);

            foreach (var entry in zip.Entries.Where(x => !x.FullName.EndsWith('/')))
            {
                total += entry.Length;
                AssetLimits.CheckInflatedSize(total, "The .nitro bundle");

                using var stream = entry.Open();
                using var output = new MemoryStream((int)entry.Length);

                stream.CopyTo(output);
                files[entry.FullName] = output.ToArray();
            }
        }
        catch (InvalidDataException ex)
        {
            throw new AssetFormatException("The .nitro bundle is not a readable zip.", ex);
        }

        return new NitroBundle(files);
    }

    /// <summary>
    /// The asset data: the <c>.json</c> that is not a spritesheet, the one named after the bundle
    /// when there are several. Null when there is none.
    /// </summary>
    public JsonObject? AssetData(string? name = null)
    {
        var candidates = Files
            .Keys.Where(x =>
                x.EndsWith(".json", StringComparison.Ordinal)
                && !x.EndsWith(SPRITESHEET_SUFFIX, StringComparison.Ordinal)
            )
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var file =
            candidates.FirstOrDefault(x => x == $"{name}.json") ?? candidates.FirstOrDefault();

        if (file is null)
            return null;

        try
        {
            return JsonNode.Parse(Files[file]) as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new AssetFormatException($"The bundle's {file} is not JSON.", ex);
        }
    }
}
