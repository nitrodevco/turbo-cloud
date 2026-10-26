using System;
using System.Buffers.Binary;
using System.IO;

namespace Turbo.Admin.Media;

/// <summary>
/// An image's type and size, read from the first bytes of its file as each format writes them:
/// PNG's header chunk, GIF's screen, JPEG's frame and WebP's frame header.
/// </summary>
public static class MediaImageSize
{
    /// <summary>The bytes of a file read to tell its size: a JPEG may put its frame after its metadata.</summary>
    public const int HEAD_BYTES = 64 * 1024;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The image type the bytes start as (<c>png</c>, <c>gif</c>, <c>jpeg</c>, <c>webp</c>); null for none of them.</summary>
    public static string? TypeOf(ReadOnlySpan<byte> head) =>
        head.StartsWith(Png) ? "png"
        : head.Length >= 6
        && (head[..6].SequenceEqual("GIF87a"u8) || head[..6].SequenceEqual("GIF89a"u8))
            ? "gif"
        : head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF ? "jpeg"
        : head.Length >= 12
        && head[..4].SequenceEqual("RIFF"u8)
        && head[8..12].SequenceEqual("WEBP"u8)
            ? "webp"
        : null;

    /// <summary>The content type of an image type <see cref="TypeOf"/> told.</summary>
    public static string ContentTypeOf(string? type) =>
        type is null ? "application/octet-stream" : $"image/{type}";

    /// <summary>The width and height the bytes say; null when they are no image this reads, or cut short.</summary>
    public static (int Width, int Height)? SizeOf(ReadOnlySpan<byte> head)
    {
        switch (TypeOf(head))
        {
            case "png" when head.Length >= 24:
                return (
                    BinaryPrimitives.ReadInt32BigEndian(head[16..]),
                    BinaryPrimitives.ReadInt32BigEndian(head[20..])
                );
            case "gif" when head.Length >= 10:
                return (
                    BinaryPrimitives.ReadUInt16LittleEndian(head[6..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(head[8..])
                );
            case "jpeg":
                return JpegSize(head);
            case "webp" when head.Length >= 30:
                return WebpSize(head);
            default:
                return null;
        }
    }

    /// <summary>The size of the image in a file; null when it is none this reads.</summary>
    public static (int Width, int Height)? SizeOfFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[(int)Math.Min(HEAD_BYTES, stream.Length)];

            stream.ReadExactly(head);

            return SizeOf(head);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (int, int)? JpegSize(ReadOnlySpan<byte> data)
    {
        var at = 2;

        while (at + 9 < data.Length)
        {
            if (data[at] != 0xFF)
                return null;

            var marker = data[at + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(at + 2)..]);

            // Every start-of-frame marker but DHT (C4), JPG (C8) and DAC (CC) gives the size.
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return (
                    BinaryPrimitives.ReadUInt16BigEndian(data[(at + 7)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(data[(at + 5)..])
                );

            at += 2 + length;
        }

        return null;
    }

    private static (int, int)? WebpSize(ReadOnlySpan<byte> data)
    {
        var chunk = data[12..16];

        if (chunk.SequenceEqual("VP8X"u8))
            return (1 + ReadUInt24(data[24..]), 1 + ReadUInt24(data[27..]));

        if (chunk.SequenceEqual("VP8 "u8))
            return (
                BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF
            );

        if (chunk.SequenceEqual("VP8L"u8))
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);

            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        return null;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> data) =>
        data[0] | data[1] << 8 | data[2] << 16;
}
