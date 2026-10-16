using System;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Turbo.Assets.Conversion;

/// <summary>
/// An image as unpremultiplied RGBA bytes, four a pixel, row after row. Images stay as bytes from
/// decoding to the packed sheet - copied, never drawn - so no pixel is changed by blending, and
/// Skia is used only to decode what an SWF or a <c>.hab</c> stores and to encode the sheet.
/// </summary>
public sealed class RgbaImage
{
    /// <summary>The largest image read: past it, a file is lying about its size.</summary>
    public const int MAX_SIDE = 8192;

    public RgbaImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || width > MAX_SIDE || height > MAX_SIDE)
            throw new AssetFormatException(
                $"An image of {width}x{height} is not one this reader takes."
            );

        if (pixels.Length != width * height * 4)
            throw new AssetFormatException("An image's pixels do not match its size.");

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public byte Alpha(int x, int y) => Pixels[(((y * Width) + x) * 4) + 3];

    /// <summary>A PNG, JPEG or GIF, decoded to unpremultiplied RGBA.</summary>
    public static RgbaImage Decode(byte[] data)
    {
        using var codec =
            SKCodec.Create(new SKMemoryStream(data))
            ?? throw new AssetFormatException(
                "An image is not a PNG, JPEG or GIF this reader can decode."
            );

        var size = codec.Info;

        if (size.Width <= 0 || size.Height <= 0 || size.Width > MAX_SIDE || size.Height > MAX_SIDE)
            throw new AssetFormatException(
                $"An image of {size.Width}x{size.Height} is not one this reader takes."
            );

        var info = new SKImageInfo(
            size.Width,
            size.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul
        );
        var pixels = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            var result = codec.GetPixels(info, handle.AddrOfPinnedObject());

            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                throw new AssetFormatException($"An image could not be decoded ({result}).");
        }
        finally
        {
            handle.Free();
        }

        return new RgbaImage(size.Width, size.Height, pixels);
    }

    /// <summary>The image as a PNG.</summary>
    /// <summary>
    /// A part of the image as one of its own <paramref name="outWidth"/> by
    /// <paramref name="outHeight"/>, the part placed at <paramref name="atX"/>,<paramref name="atY"/>
    /// in it (a trimmed spritesheet frame put back where its source had it); outside the image is
    /// transparent.
    /// </summary>
    public RgbaImage Crop(
        int x,
        int y,
        int width,
        int height,
        int outWidth,
        int outHeight,
        int atX = 0,
        int atY = 0
    )
    {
        var pixels = new byte[outWidth * outHeight * 4];

        for (var row = 0; row < height; row++)
        {
            var sourceY = y + row;
            var targetY = atY + row;

            if (sourceY < 0 || sourceY >= Height || targetY < 0 || targetY >= outHeight)
                continue;

            for (var column = 0; column < width; column++)
            {
                var sourceX = x + column;
                var targetX = atX + column;

                if (sourceX < 0 || sourceX >= Width || targetX < 0 || targetX >= outWidth)
                    continue;

                Array.Copy(
                    Pixels,
                    ((sourceY * Width) + sourceX) * 4,
                    pixels,
                    ((targetY * outWidth) + targetX) * 4,
                    4
                );
            }
        }

        return new RgbaImage(outWidth, outHeight, pixels);
    }

    public byte[] EncodePng()
    {
        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var handle = GCHandle.Alloc(Pixels, GCHandleType.Pinned);

        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            using var data =
                pixmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Skia could not encode the sheet as PNG.");

            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Whether two images are the same pixels.</summary>
    public bool SameAs(RgbaImage other) =>
        Width == other.Width
        && Height == other.Height
        && Pixels.AsSpan().SequenceEqual(other.Pixels);
}
