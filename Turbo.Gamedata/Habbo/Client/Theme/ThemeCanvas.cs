using System;
using Turbo.Assets.Conversion;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// Drawing a skin's regions as the client's <c>BitmapSkinRenderer</c> does and Studio's
/// <c>skinArt.ts</c> copies it: nearest-neighbour (the renderer's smoothing is off), alpha over,
/// with Studio's arithmetic and rounding so every pixel comes out as Studio's does.
/// </summary>
internal static class ThemeCanvas
{
    /// <summary>A transparent image, at least one pixel each way.</summary>
    public static RgbaImage Blank(double width, double height)
    {
        var w = (int)Math.Max(1, width);
        var h = (int)Math.Max(1, height);

        return new RgbaImage(w, h, new byte[w * h * 4]);
    }

    /// <summary>A region of <paramref name="source"/> drawn over <paramref name="target"/> at <paramref name="to"/>, scaled to it.</summary>
    public static void DrawRegion(RgbaImage source, ThemeRect from, RgbaImage target, ThemeRect to)
    {
        var src = source.Pixels;
        var dst = target.Pixels;

        for (double y = 0; y < to.Height; y++)
        {
            var ty = to.Y + y;

            if (!(ty >= 0 && ty < target.Height))
                continue;

            var sy = from.Y + Math.Min(from.Height - 1, Math.Floor(y * from.Height / to.Height));

            for (double x = 0; x < to.Width; x++)
            {
                var tx = to.X + x;

                if (!(tx >= 0 && tx < target.Width))
                    continue;

                var sx = from.X + Math.Min(from.Width - 1, Math.Floor(x * from.Width / to.Width));

                // A side that is not a number reads nothing, as an out-of-range index does in Studio.
                if (!(sx >= 0 && sy >= 0 && sx < source.Width && sy < source.Height))
                    continue;

                var s = ((((int)sy) * source.Width) + (int)sx) * 4;
                var t = ((((int)ty) * target.Width) + (int)tx) * 4;
                var alpha = src[s + 3] / 255d;

                if (alpha == 0)
                    continue;

                var under = dst[t + 3] / 255d;
                var output = alpha + (under * (1 - alpha));
                var divisor = output == 0 ? 1 : output;

                for (var channel = 0; channel < 3; channel++)
                {
                    dst[t + channel] = Round(
                        ((src[s + channel] * alpha) + (dst[t + channel] * under * (1 - alpha)))
                            / divisor
                    );
                }

                dst[t + 3] = Round(output * 255);
            }
        }
    }

    /// <summary>
    /// Each sprite's own edge row and column copied one pixel out, into the gutter round it, so art
    /// drawn stretched past its size samples its own edge and not its neighbour's.
    /// </summary>
    public static void ExtrudeEdges(RgbaImage sheet, int x, int y, int width, int height)
    {
        for (var column = 0; column < width; column++)
        {
            CopyPixel(sheet, x + column, y, x + column, y - 1);
            CopyPixel(sheet, x + column, y + height - 1, x + column, y + height);
        }

        for (var row = -1; row <= height; row++)
        {
            var source = Math.Max(0, Math.Min(height - 1, row));

            CopyPixel(sheet, x, y + source, x - 1, y + row);
            CopyPixel(sheet, x + width - 1, y + source, x + width, y + row);
        }
    }

    /// <summary>One image copied into another at a place, pixels as they are (no blending).</summary>
    public static void Blit(RgbaImage source, RgbaImage target, int x, int y)
    {
        for (var row = 0; row < source.Height; row++)
            Array.Copy(
                source.Pixels,
                row * source.Width * 4,
                target.Pixels,
                (((y + row) * target.Width) + x) * 4,
                source.Width * 4
            );
    }

    private static void CopyPixel(RgbaImage image, int sx, int sy, int dx, int dy) =>
        Array.Copy(
            image.Pixels,
            ((sy * image.Width) + sx) * 4,
            image.Pixels,
            ((dy * image.Width) + dx) * 4,
            4
        );

    /// <summary><c>Math.round</c> for the non-negative values a blend gives, stored as a byte.</summary>
    private static byte Round(double value) =>
        (byte)(int)Math.Round(value, MidpointRounding.AwayFromZero);
}
