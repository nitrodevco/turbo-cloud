using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Turbo.Assets.Asar;

/// <summary>
/// An Electron <c>app.asar</c>, as Habbo's HTML client ships in one: a little-endian header
/// (<c>4</c>, the header pickle's size, the payload size, the JSON's length), the JSON tree of
/// files at offset 16, and the files' bytes from <c>8 + headerSize</c> on. A file is read from
/// disk when asked for, and checked against its <c>integrity.hash</c> (SHA-256) when it has one.
/// Unpacked files (kept beside the archive in <c>app.asar.unpacked</c>) and links are not read.
/// </summary>
public sealed class AsarArchive
{
    /// <summary>The largest header taken: Habbo's is some hundreds of kilobytes.</summary>
    private const int MAX_HEADER = 64 * 1024 * 1024;

    private readonly string _path;
    private readonly long _dataStart;
    private readonly Dictionary<string, AsarFile> _files;

    private AsarArchive(string path, long dataStart, Dictionary<string, AsarFile> files)
    {
        _path = path;
        _dataStart = dataStart;
        _files = files;
    }

    /// <summary>Every file's path (<c>/</c> between folders, no leading one).</summary>
    public IReadOnlyCollection<string> Files => _files.Keys;

    public bool Has(string path) => _files.ContainsKey(path);

    /// <summary>Opens the archive at <paramref name="path"/>, reading its header only.</summary>
    public static AsarArchive Open(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[16];

        if (file.Read(head) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(head) != 4)
            throw new AssetFormatException("The file is not an asar archive.");

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
        var jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(head[12..]);

        if (jsonLength > headerSize || jsonLength > MAX_HEADER)
            throw new AssetFormatException("The asar archive's header is corrupt.");

        var json = new byte[jsonLength];

        file.ReadExactly(json);

        var files = new Dictionary<string, AsarFile>(StringComparer.Ordinal);

        using (var document = JsonDocument.Parse(json))
            Walk(document.RootElement, "", files);

        return new AsarArchive(path, 8L + headerSize, files);
    }

    /// <summary>A file's bytes; null when the archive has none at that path.</summary>
    public byte[]? Read(string path)
    {
        if (!_files.TryGetValue(path, out var entry))
            return null;

        if (entry.Unpacked)
            throw new AssetFormatException(
                $"{path} is kept outside the asar archive, which is not read."
            );

        if (entry.Size > int.MaxValue)
            throw new AssetFormatException($"{path} is too large to read.");

        var data = new byte[entry.Size];

        using (var file = File.OpenRead(_path))
        {
            file.Position = _dataStart + entry.Offset;

            if (file.ReadAtLeast(data, data.Length, throwOnEndOfStream: false) != data.Length)
                throw new AssetFormatException($"{path} is cut short in the asar archive.");
        }

        if (
            entry.Sha256 is { } hash
            && !string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(data)),
                hash,
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new AssetFormatException(
                $"{path} does not match the hash the asar archive gives it."
            );

        return data;
    }

    /// <summary>A file's bytes as UTF-8 text; null when there is none.</summary>
    public string? ReadText(string path) =>
        Read(path) is { } data ? Encoding.UTF8.GetString(data) : null;

    private static void Walk(JsonElement node, string prefix, Dictionary<string, AsarFile> files)
    {
        if (
            !node.TryGetProperty("files", out var children)
            || children.ValueKind != JsonValueKind.Object
        )
            return;

        foreach (var child in children.EnumerateObject())
        {
            var path = prefix.Length == 0 ? child.Name : $"{prefix}/{child.Name}";
            var value = child.Value;

            if (value.ValueKind != JsonValueKind.Object || value.TryGetProperty("link", out _))
                continue;

            if (value.TryGetProperty("files", out _))
            {
                Walk(value, path, files);

                continue;
            }

            var size = value.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0;
            var offset =
                value.TryGetProperty("offset", out var o)
                && long.TryParse(
                    o.ValueKind == JsonValueKind.String ? o.GetString() : o.GetRawText(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var at
                )
                    ? at
                    : 0;
            var unpacked =
                value.TryGetProperty("unpacked", out var u) && u.ValueKind == JsonValueKind.True;
            string? sha256 = null;

            if (
                value.TryGetProperty("integrity", out var integrity)
                && integrity.TryGetProperty("algorithm", out var algorithm)
                && algorithm.GetString() == "SHA256"
                && integrity.TryGetProperty("hash", out var hash)
            )
                sha256 = hash.GetString();

            files[path] = new AsarFile(size, offset, unpacked, sha256);
        }
    }

    private sealed record AsarFile(long Size, long Offset, bool Unpacked, string? Sha256);
}
