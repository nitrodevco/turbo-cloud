using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Turbo.Assets.Swf;

/// <summary>
/// A Habbo SWF asset library: its symbol table and binary tags, the documents (XML, palettes) a
/// library carries, and its image tags kept as they are for the converter. Only the top-level tags
/// are read; Habbo's libraries keep everything there.
/// <para>
/// An SWF is <c>FWS</c> (plain), <c>CWS</c> (zlib after the 8-byte header) or <c>ZWS</c> (LZMA,
/// which this reader refuses: Habbo's furniture comes as <c>CWS</c>).
/// </para>
/// </summary>
public sealed class SwfLibrary : IAssetLibrary
{
    public const int TAG_END = 0;
    public const int TAG_DEFINE_BITS_JPEG2 = 21;
    public const int TAG_DEFINE_BITS_LOSSLESS = 20;
    public const int TAG_DEFINE_BITS_JPEG3 = 35;
    public const int TAG_DEFINE_BITS_LOSSLESS2 = 36;
    public const int TAG_SYMBOL_CLASS = 76;
    public const int TAG_DEFINE_BINARY_DATA = 87;

    private const int HEADER_LENGTH = 8;

    // A tag's length is a 6-bit field; this value says a 32-bit length follows.
    private const int LONG_TAG = 0x3f;

    private readonly Dictionary<string, int> _characterByName = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _nameByCharacter = [];
    private readonly HashSet<string> _assignedNames = new(StringComparer.Ordinal);
    private readonly List<(int Id, string Name)> _symbols = [];
    private readonly Dictionary<int, byte[]> _binaries = [];
    private readonly List<SwfImageTag> _images = [];

    private SwfLibrary(int version)
    {
        Version = version;
    }

    public int Version { get; }

    public string DocumentClass { get; private set; } = string.Empty;

    /// <summary>The image tags, by character id, as stored: for the converter to decode.</summary>
    public IReadOnlyList<SwfImageTag> Images => _images;

    /// <summary>Every exported symbol, in the order the symbol tables list them.</summary>
    public IReadOnlyList<(int Id, string Name)> Symbols => _symbols;

    /// <summary>
    /// A character's class name: the first name the symbol tables give it that no character before
    /// it took, as Habbo's own SWF reader assigns them. Null for a character with none.
    /// </summary>
    public string? NameOf(int characterId) => _nameByCharacter.GetValueOrDefault(characterId);

    public static bool IsSwf(ReadOnlySpan<byte> data) =>
        data.Length >= HEADER_LENGTH
        && data[1] == (byte)'W'
        && data[2] == (byte)'S'
        && data[0] is (byte)'F' or (byte)'C' or (byte)'Z';

    public static SwfLibrary Read(byte[] data)
    {
        if (!IsSwf(data))
            throw new AssetFormatException(
                "Not an SWF: the file does not start with FWS, CWS or ZWS."
            );

        var library = new SwfLibrary(data[3]);
        var body = Body(data);
        var offset = SkipMovieHeader(body);

        while (offset + 2 <= body.Length)
        {
            var codeAndLength = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(offset));
            var code = codeAndLength >> 6;
            long length = codeAndLength & LONG_TAG;

            offset += 2;

            if (length == LONG_TAG)
            {
                if (offset + 4 > body.Length)
                    throw new AssetFormatException("The SWF is truncated in a tag header.");

                length = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(offset));
                offset += 4;
            }

            if (code == TAG_END)
                break;

            if (offset + length > body.Length)
                throw new AssetFormatException($"The SWF is truncated in tag {code}.");

            var tag = body.AsSpan(offset, (int)length);

            library.ReadTag(code, tag);
            offset += (int)length;
        }

        return library;
    }

    public string? GetXml(string name) =>
        GetBinary($"{DocumentClass}_{name}") is { } bytes ? Encoding.UTF8.GetString(bytes) : null;

    /// <summary>
    /// A binary tag by its exported name. One character can be exported under several names
    /// (croco's twelve palettes are one binary), so every name in the symbol table counts.
    /// </summary>
    public byte[]? GetBinary(string name) =>
        _characterByName.TryGetValue(name, out var id) && _binaries.TryGetValue(id, out var data)
            ? data
            : null;

    private void ReadTag(int code, ReadOnlySpan<byte> tag)
    {
        switch (code)
        {
            case TAG_SYMBOL_CLASS:
                ReadSymbols(tag);
                break;
            case TAG_DEFINE_BINARY_DATA:
                // UI16 character id, UI32 reserved, then the data.
                if (tag.Length >= 6)
                    _binaries[BinaryPrimitives.ReadUInt16LittleEndian(tag)] = tag[6..].ToArray();
                break;
            case TAG_DEFINE_BITS_LOSSLESS
            or TAG_DEFINE_BITS_LOSSLESS2
            or TAG_DEFINE_BITS_JPEG2
            or TAG_DEFINE_BITS_JPEG3:
                if (tag.Length >= 2)
                    _images.Add(
                        new SwfImageTag(
                            BinaryPrimitives.ReadUInt16LittleEndian(tag),
                            code,
                            tag[2..].ToArray()
                        )
                    );
                break;
        }
    }

    private void ReadSymbols(ReadOnlySpan<byte> tag)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(tag);
        var offset = 2;

        for (var i = 0; i < count && offset + 2 <= tag.Length; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(tag[offset..]);

            offset += 2;

            var end = tag[offset..].IndexOf((byte)0);

            if (end < 0)
                throw new AssetFormatException("The SWF's symbol table is truncated.");

            var name = Encoding.UTF8.GetString(tag.Slice(offset, end));

            offset += end + 1;

            // The document class is the symbol of character 0; a name keeps its first character.
            if (id == 0 && DocumentClass.Length == 0)
                DocumentClass = name;

            _characterByName.TryAdd(name, id);
            _symbols.Add((id, name));

            if (!_nameByCharacter.ContainsKey(id) && _assignedNames.Add(name))
                _nameByCharacter[id] = name;
        }
    }

    private static byte[] Body(byte[] data)
    {
        var fileLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));

        switch ((char)data[0])
        {
            case 'F':
                return data[HEADER_LENGTH..];
            case 'C':
            {
                AssetLimits.CheckInflatedSize(fileLength, "The SWF");

                var expected = (int)Math.Max(0, fileLength - HEADER_LENGTH);
                var body = new byte[expected];

                using var input = new MemoryStream(
                    data,
                    HEADER_LENGTH,
                    data.Length - HEADER_LENGTH
                );
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);

                try
                {
                    zlib.ReadAtLeast(body, expected, throwOnEndOfStream: false);
                }
                catch (InvalidDataException ex)
                {
                    throw new AssetFormatException("The SWF's compressed body is corrupt.", ex);
                }

                return body;
            }
            default:
                throw new AssetFormatException(
                    "The SWF is LZMA-compressed (ZWS), which this reader does not open yet."
                );
        }
    }

    /// <summary>Past the frame size (a bit-packed RECT), frame rate and frame count.</summary>
    private static int SkipMovieHeader(byte[] body)
    {
        if (body.Length < 1)
            throw new AssetFormatException("The SWF has no body.");

        var bits = body[0] >> 3;
        var rectBytes = (5 + (bits * 4) + 7) / 8;

        return rectBytes + 4;
    }
}
