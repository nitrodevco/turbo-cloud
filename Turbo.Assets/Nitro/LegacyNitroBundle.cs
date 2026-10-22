using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Turbo.Assets.Nitro;

/// <summary>
/// A <c>.nitro</c> bundle of the old layout, from before bundles were zips. Big-endian:
/// <code>
/// 0   2  how many files
///        then for each file:
///     2  its name's length
///        its name (UTF-8)
///     4  its data's length
///        its data, zlib'd
/// </code>
/// The asset data in it is of the old layout too: its collections are objects keyed by name or
/// id, and the spritesheet is inside it (<see cref="Conversion.NitroConverter"/> brings it to the
/// current one).
/// </summary>
public static class LegacyNitroBundle
{
    // The first byte of a zlib stream: deflate with a window of up to 32K (0x08 | 0x70).
    private const int ZLIB_DEFLATE = 0x08;

    /// <summary>
    /// Whether the file is laid out as an old bundle, to its last byte: a count, then that many
    /// files that each fit, start as a zlib stream does, and end where the file does. Nothing is
    /// inflated to tell.
    /// </summary>
    public static bool IsLegacy(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            return false;

        var count = BinaryPrimitives.ReadUInt16BigEndian(data);
        var offset = 2;

        if (count == 0)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (offset + 2 > data.Length)
                return false;

            var nameLength = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

            offset += 2;

            if (nameLength == 0 || offset + nameLength + 4 > data.Length)
                return false;

            offset += nameLength;

            var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);

            offset += 4;

            if (length < 2 || length > (uint)(data.Length - offset) || !IsZlib(data[offset..]))
                return false;

            offset += (int)length;
        }

        return offset == data.Length;
    }

    /// <summary>The files, by name, inflated.</summary>
    public static IReadOnlyDictionary<string, byte[]> Read(byte[] data)
    {
        if (!IsLegacy(data))
            throw new AssetFormatException("Not a .nitro bundle of the old layout.");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var count = BinaryPrimitives.ReadUInt16BigEndian(data);
        var offset = 2;
        long total = 0;

        for (var i = 0; i < count; i++)
        {
            var nameLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

            offset += 2;

            string name;

            try
            {
                name = new UTF8Encoding(false, true).GetString(data, offset, nameLength);
            }
            catch (DecoderFallbackException ex)
            {
                throw new AssetFormatException("A file in the old .nitro bundle has no name.", ex);
            }

            offset += nameLength;

            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));

            offset += 4;

            var inflated = Inflate(
                data,
                offset,
                length,
                name,
                AssetLimits.MAX_INFLATED_BYTES - total
            );

            total += inflated.Length;
            files[name] = inflated;
            offset += length;
        }

        return files;
    }

    private static bool IsZlib(ReadOnlySpan<byte> data) =>
        (data[0] & 0x0F) == ZLIB_DEFLATE && ((data[0] << 8) | data[1]) % 31 == 0;

    /// <summary>One file's data, refused once it unpacks past what the bundle may still hold.</summary>
    private static byte[] Inflate(byte[] data, int offset, int length, string name, long room)
    {
        try
        {
            using var input = new MemoryStream(data, offset, length);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;

            while ((read = zlib.Read(buffer)) > 0)
            {
                output.Write(buffer, 0, read);

                if (output.Length > room)
                    throw new AssetFormatException(
                        $"The old .nitro bundle unpacks to more than the {AssetLimits.MAX_INFLATED_BYTES} bytes allowed."
                    );
            }

            return output.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new AssetFormatException($"The old .nitro bundle's {name} is corrupt.", ex);
        }
    }
}
