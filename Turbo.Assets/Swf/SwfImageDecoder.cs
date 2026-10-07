using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using Turbo.Assets.Conversion;

namespace Turbo.Assets.Swf;

/// <summary>
/// An SWF image tag decoded, as the studio's converter decoded it:
/// <list type="bullet">
/// <item>DefineBitsLossless2: 32-bit premultiplied ARGB, unpremultiplied; or 8-bit with an RGBA
/// colour table.</item>
/// <item>DefineBitsLossless: the same without alpha (its colour table is RGB).</item>
/// <item>DefineBitsJPEG2: a JPEG (or PNG, or GIF) as it is.</item>
/// <item>DefineBitsJPEG3: a JPEG with a separate zlib'd alpha plane.</item>
/// </list>
/// </summary>
public static class SwfImageDecoder
{
    private const int FORMAT_COLORMAPPED = 3;
    private const int FORMAT_ARGB = 5;

    public static RgbaImage Decode(SwfImageTag tag) =>
        tag.Code switch
        {
            SwfLibrary.TAG_DEFINE_BITS_LOSSLESS => Lossless(tag.Data, alpha: false),
            SwfLibrary.TAG_DEFINE_BITS_LOSSLESS2 => Lossless(tag.Data, alpha: true),
            SwfLibrary.TAG_DEFINE_BITS_JPEG2 => RgbaImage.Decode(StripBadHeader(tag.Data)),
            SwfLibrary.TAG_DEFINE_BITS_JPEG3 => Jpeg3(tag.Data),
            _ => throw new AssetFormatException($"SWF tag {tag.Code} is not an image."),
        };

    private static RgbaImage Lossless(byte[] data, bool alpha)
    {
        if (data.Length < 5)
            throw new AssetFormatException("An SWF bitmap is truncated.");

        int format = data[0];
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(1));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(3));
        var offset = 5;
        var colors = 0;

        if (format == FORMAT_COLORMAPPED)
        {
            colors = data[offset] + 1;
            offset++;
        }

        if (width == 0 || height == 0 || width > RgbaImage.MAX_SIDE || height > RgbaImage.MAX_SIDE)
            throw new AssetFormatException(
                $"An SWF bitmap of {width}x{height} is not one this reader takes."
            );

        var entry = alpha ? 4 : 3;
        var expected =
            format == FORMAT_COLORMAPPED
                ? (colors * entry) + (((width + 3) & ~3) * height)
                : width * height * 4;
        var raw = Inflate(data.AsSpan(offset).ToArray(), expected);
        var output = new byte[width * height * 4];

        switch (format)
        {
            case FORMAT_ARGB:
            {
                for (int i = 0, p = 0; i < output.Length && p + 3 < raw.Length; i += 4, p += 4)
                {
                    if (!alpha)
                    {
                        // PIX24: a reserved byte, then RGB, opaque.
                        output[i] = raw[p + 1];
                        output[i + 1] = raw[p + 2];
                        output[i + 2] = raw[p + 3];
                        output[i + 3] = 255;

                        continue;
                    }

                    var a = raw[p];

                    // Unpremultiplied as the studio did: c * (255 / a), truncated; nothing where a is 0.
                    output[i] = Unpremultiply(raw[p + 1], a);
                    output[i + 1] = Unpremultiply(raw[p + 2], a);
                    output[i + 2] = Unpremultiply(raw[p + 3], a);
                    output[i + 3] = a;
                }

                break;
            }
            case FORMAT_COLORMAPPED:
            {
                var table = new byte[colors * 4];

                for (var c = 0; c < colors && (c * entry) + entry <= raw.Length; c++)
                {
                    table[c * 4] = raw[c * entry];
                    table[(c * 4) + 1] = raw[(c * entry) + 1];
                    table[(c * 4) + 2] = raw[(c * entry) + 2];
                    table[(c * 4) + 3] = alpha ? raw[(c * entry) + 3] : (byte)255;
                }

                var p = colors * entry;
                var padding = (4 - (width % 4)) % 4;

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++, p++)
                    {
                        if (p >= raw.Length)
                            break;

                        var index = raw[p];

                        // An index past the table is transparent.
                        if (index < colors)
                            Array.Copy(table, index * 4, output, ((y * width) + x) * 4, 4);
                    }

                    p += padding;
                }

                break;
            }
            default:
                throw new AssetFormatException(
                    $"SWF bitmap format {format} is not one this reader takes."
                );
        }

        return new RgbaImage(width, height, output);
    }

    private static RgbaImage Jpeg3(byte[] data)
    {
        if (data.Length < 4)
            throw new AssetFormatException("An SWF JPEG is truncated.");

        var alphaOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data);

        if (alphaOffset < 0 || 4 + alphaOffset > data.Length)
            throw new AssetFormatException("An SWF JPEG's alpha is past its end.");

        var image = StripBadHeader(data.AsSpan(4, alphaOffset).ToArray());
        var decoded = RgbaImage.Decode(image);

        // A PNG or a GIF carries its own alpha; only a JPEG takes the plane after it.
        if (image.Length < 2 || image[0] != 0xFF || image[1] != 0xD8)
            return decoded;

        var pixels = decoded.Width * decoded.Height;
        var alphaData = data.AsSpan(4 + alphaOffset).ToArray();

        if (alphaData.Length == 0)
            return decoded;

        var alpha = Inflate(alphaData, pixels);

        for (var i = 0; i < pixels && i < alpha.Length; i++)
            decoded.Pixels[(i * 4) + 3] = alpha[i];

        return decoded;
    }

    /// <summary>Old SWFs put an end-of-image marker before the start (FF D9 FF D8): not part of the JPEG.</summary>
    private static byte[] StripBadHeader(byte[] data) =>
        data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD9 && data[2] == 0xFF && data[3] == 0xD8
            ? data[4..]
            : data;

    private static byte Unpremultiply(byte value, byte alpha) =>
        alpha == 0 ? (byte)0 : (byte)Math.Min(255, (int)(value * (255d / alpha)));

    /// <summary>zlib data, at most <paramref name="expected"/> bytes of it (and a little slack, as the studio allowed).</summary>
    private static byte[] Inflate(byte[] data, int expected)
    {
        var limit = expected + 1024;
        var output = new byte[limit];

        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);

            var read = zlib.ReadAtLeast(output, limit, throwOnEndOfStream: false);

            return output[..read];
        }
        catch (InvalidDataException ex)
        {
            throw new AssetFormatException("An SWF image's compressed data is corrupt.", ex);
        }
    }
}
