using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Turbo.Assets.Hab;

/// <summary>
/// The HTML client's asset library, its replacement for an SWF. Little-endian:
/// <code>
/// 0   4  "HAB\0"
/// 4   2  format version (1)
/// 6   2  flags
/// 8   4  manifest length, stored
/// 12  4  manifest length, inflated
/// 16  4  payload length
/// 20     the manifest (zlib'd JSON), then the payload, which entry offsets count from
/// </code>
/// Most carry an SWF's own documents - its <c>manifest</c> XML in the manifest, and <c>index</c>,
/// <c>&lt;type&gt;_visualization</c>, ... and the images as entries named by asset. The small
/// generic ones carry a prebuilt <c>&lt;Name&gt;.json</c> in the old Nitro layout instead.
/// </summary>
public sealed class HabLibrary : IAssetLibrary
{
    private const int HEADER_LENGTH = 20;
    private const int SUPPORTED_VERSION = 1;
    private const string XML_TYPE = "text/xml";

    private static readonly byte[] MAGIC = "HAB\0"u8.ToArray();
    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    private readonly byte[] _data;
    private readonly int _payloadStart;
    private readonly int _payloadLength;
    private readonly Dictionary<string, HabEntry> _entries;

    private HabLibrary(
        HabManifest manifest,
        byte[] data,
        int payloadStart,
        int payloadLength,
        string? documentClass
    )
    {
        Manifest = manifest;
        _data = data;
        _payloadStart = payloadStart;
        _payloadLength = payloadLength;
        _entries = manifest.Entries.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        DocumentClass = documentClass ?? manifest.Name;
    }

    public HabManifest Manifest { get; }

    /// <summary>
    /// The library's name as the client asks for it (<c>HabboRoomContent</c>), which is not
    /// always the manifest's (<c>room</c>), so a caller may give it.
    /// </summary>
    public string DocumentClass { get; }

    public IReadOnlyCollection<HabEntry> Entries => _entries.Values;

    public static bool IsHab(ReadOnlySpan<byte> data) =>
        data.Length >= HEADER_LENGTH && data[..4].SequenceEqual(MAGIC);

    public static HabLibrary Read(byte[] data, string? documentClass = null)
    {
        if (!IsHab(data))
            throw new AssetFormatException("Not a .hab library: the file does not start with HAB.");

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));

        if (version != SUPPORTED_VERSION)
            throw new AssetFormatException($"Unsupported .hab version {version}.");

        var manifestLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
        var manifestInflated = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        var payloadLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
        var payloadStart = HEADER_LENGTH + manifestLength;

        if (
            manifestLength < 0
            || payloadLength < 0
            || (long)payloadStart + payloadLength > data.Length
        )
            throw new AssetFormatException("The .hab is truncated.");

        AssetLimits.CheckInflatedSize(manifestInflated, "The .hab manifest");

        var manifestBytes = Inflate(
            data.AsSpan(HEADER_LENGTH, manifestLength).ToArray(),
            (int)manifestInflated,
            "The .hab manifest"
        );

        HabManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<HabManifest>(manifestBytes, JSON);
        }
        catch (JsonException ex)
        {
            throw new AssetFormatException("The .hab manifest is not JSON.", ex);
        }

        if (manifest is not { Format: "hab" } || manifest.Entries is null)
            throw new AssetFormatException("The .hab manifest is not a hab manifest.");

        return new HabLibrary(manifest, data, payloadStart, payloadLength, documentClass);
    }

    public string? GetXml(string name)
    {
        if (name == "manifest")
            return Manifest.ManifestXml;

        return _entries.TryGetValue(name, out var entry) && entry.MimeType == XML_TYPE
            ? Encoding.UTF8.GetString(Get(entry))
            : null;
    }

    public byte[]? GetBinary(string name) =>
        _entries.TryGetValue(name, out var entry) ? Get(entry) : null;

    /// <summary>An entry's contents, inflated.</summary>
    public byte[] Get(HabEntry entry)
    {
        if (
            entry.Offset < 0
            || entry.StoredLength < 0
            || (long)entry.Offset + entry.StoredLength > _payloadLength
        )
            throw new AssetFormatException($"The .hab entry {entry.Name} is truncated.");

        var stored = _data.AsSpan(_payloadStart + entry.Offset, entry.StoredLength).ToArray();

        AssetLimits.CheckInflatedSize(entry.OriginalLength, $"The .hab entry {entry.Name}");

        var data = entry.Compression switch
        {
            "none" => stored,
            "deflate" => Inflate(stored, entry.OriginalLength, $"The .hab entry {entry.Name}"),
            _ => throw new AssetFormatException(
                $"The .hab entry {entry.Name} uses an unknown compression ({entry.Compression})."
            ),
        };

        if (data.Length != entry.OriginalLength)
            throw new AssetFormatException($"The .hab entry {entry.Name} is corrupt.");

        return data;
    }

    private static byte[] Inflate(byte[] stored, int expected, string what)
    {
        var output = new byte[expected];

        try
        {
            using var input = new MemoryStream(stored);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);

            var read = zlib.ReadAtLeast(output, expected, throwOnEndOfStream: false);

            // Anything past what it said it holds is more than it may unpack to.
            if (read != expected || zlib.ReadByte() != -1)
                throw new AssetFormatException($"{what} is corrupt.");
        }
        catch (InvalidDataException ex)
        {
            throw new AssetFormatException($"{what} is corrupt.", ex);
        }

        return output;
    }
}
