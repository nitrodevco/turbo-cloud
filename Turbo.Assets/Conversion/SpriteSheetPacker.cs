using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Turbo.Assets.Conversion;

/// <summary>
/// The images a bundle uses packed into one sheet, with its frames in the shape the client's Pixi
/// loader reads (the "Pixi" layout free-tex-packer wrote for the studio):
/// <list type="bullet">
/// <item>each image trimmed of its transparent edges, the frame saying where it sat
/// (<c>spriteSourceSize</c>, <c>sourceSize</c>); an image with nothing in it is left whole;</item>
/// <item>images with the same pixels share one place on the sheet;</item>
/// <item>nothing rotated, nothing padded; the sheet as small as its images.</item>
/// </list>
/// Places are found by MaxRects, best short side fit, tallest first.
/// </summary>
public static class SpriteSheetPacker
{
    public const int MAX_WIDTH = 10240;
    public const int MAX_HEIGHT = 4320;

    public sealed record Result(RgbaImage Sheet, JsonObject Frames);

    public static Result? Pack(ImageBundle bundle, string sheetName)
    {
        var names = bundle.Referenced.Where(bundle.Images.ContainsKey).ToList();

        if (names.Count == 0)
            return null;

        // Trimmed images, one per distinct picture: names with the same pixels share.
        var distinct = new List<Trimmed>();
        var byName = new Dictionary<string, Trimmed>();
        var byContent = new Dictionary<(int, int, int), List<Trimmed>>();

        foreach (var name in names)
        {
            var image = bundle.Images[name];
            var key = (image.Width, image.Height, ContentHash(image.Pixels));
            var candidates = byContent.TryGetValue(key, out var list) ? list : byContent[key] = [];
            var known = candidates.FirstOrDefault(x => x.Source.SameAs(image));

            if (known is null)
            {
                known = Trim(image);
                candidates.Add(known);
                distinct.Add(known);
            }

            byName[name] = known;
        }

        Place(distinct);

        var width = distinct.Max(x => x.X + x.Width);
        var height = distinct.Max(x => x.Y + x.Height);
        var sheet = new byte[width * height * 4];

        foreach (var image in distinct)
            for (var row = 0; row < image.Height; row++)
                Array.Copy(
                    image.Source.Pixels,
                    (((image.Top + row) * image.Source.Width) + image.Left) * 4,
                    sheet,
                    (((image.Y + row) * width) + image.X) * 4,
                    image.Width * 4
                );

        var frames = new JsonObject();

        foreach (var name in names)
        {
            var image = byName[name];

            frames[name] = new JsonObject
            {
                ["frame"] = Rect(image.X, image.Y, image.Width, image.Height),
                ["rotated"] = false,
                ["trimmed"] = image.IsTrimmed,
                ["spriteSourceSize"] = Rect(image.Left, image.Top, image.Width, image.Height),
                ["sourceSize"] = new JsonObject
                {
                    ["w"] = image.Source.Width,
                    ["h"] = image.Source.Height,
                },
                ["pivot"] = new JsonObject { ["x"] = 0.5, ["y"] = 0.5 },
            };
        }

        var spritesheet = new JsonObject
        {
            ["frames"] = frames,
            ["meta"] = new JsonObject
            {
                ["image"] = $"{sheetName}.png",
                ["format"] = "RGBA8888",
                ["size"] = new JsonObject { ["w"] = width, ["h"] = height },
                ["scale"] = 1,
            },
        };

        return new Result(new RgbaImage(width, height, sheet), spritesheet);
    }

    private static int ContentHash(byte[] pixels)
    {
        var hash = new HashCode();

        hash.AddBytes(pixels);

        return hash.ToHashCode();
    }

    private static JsonObject Rect(int x, int y, int w, int h) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["w"] = w,
            ["h"] = h,
        };

    /// <summary>The image less its transparent edges, as free-tex-packer's trimmer cuts them.</summary>
    private static Trimmed Trim(RgbaImage image)
    {
        int left = 0,
            top = 0,
            right = image.Width - 1,
            bottom = image.Height - 1;

        while (left < image.Width && ColumnEmpty(image, left))
            left++;

        // Nothing in it at all: free-tex-packer's search for the left edge finds none and says 0,
        // so it leaves such an image whole (its 1x1 case is never reached). So does this.
        if (left == image.Width)
            return new Trimmed(image, 0, 0, image.Width, image.Height, false);

        while (ColumnEmpty(image, right))
            right--;

        while (RowEmpty(image, top))
            top++;

        while (RowEmpty(image, bottom))
            bottom--;

        var width = right - left + 1;
        var height = bottom - top + 1;

        return new Trimmed(
            image,
            left,
            top,
            width,
            height,
            width != image.Width || height != image.Height
        );
    }

    private static bool ColumnEmpty(RgbaImage image, int x)
    {
        for (var y = 0; y < image.Height; y++)
            if (image.Alpha(x, y) > 0)
                return false;

        return true;
    }

    private static bool RowEmpty(RgbaImage image, int y)
    {
        for (var x = 0; x < image.Width; x++)
            if (image.Alpha(x, y) > 0)
                return false;

        return true;
    }

    /// <summary>
    /// A place on the sheet for each image. The sheet starts as wide as the images' area needs
    /// (and the widest image), and grows wider when they don't fit within its height.
    /// </summary>
    private static void Place(List<Trimmed> images)
    {
        var area = images.Sum(x => (long)x.Width * x.Height);
        var width = Math.Max(images.Max(x => x.Width), (int)Math.Ceiling(Math.Sqrt(area)));
        var order = images.OrderByDescending(x => x.Height).ThenByDescending(x => x.Width).ToList();

        while (true)
        {
            if (TryPlace(order, Math.Min(width, MAX_WIDTH), MAX_HEIGHT))
                return;

            if (width >= MAX_WIDTH)
                throw new AssetFormatException(
                    $"The library's images do not fit on one {MAX_WIDTH}x{MAX_HEIGHT} sheet."
                );

            width = (int)Math.Ceiling(width * 1.25);
        }
    }

    private static bool TryPlace(List<Trimmed> images, int width, int height)
    {
        var free = new List<(int X, int Y, int W, int H)> { (0, 0, width, height) };

        foreach (var image in images)
        {
            var best = -1;
            var bestShort = int.MaxValue;
            var bestLong = int.MaxValue;

            for (var i = 0; i < free.Count; i++)
            {
                var (_, _, w, h) = free[i];

                if (w < image.Width || h < image.Height)
                    continue;

                var shortSide = Math.Min(w - image.Width, h - image.Height);
                var longSide = Math.Max(w - image.Width, h - image.Height);

                if (shortSide < bestShort || (shortSide == bestShort && longSide < bestLong))
                {
                    best = i;
                    bestShort = shortSide;
                    bestLong = longSide;
                }
            }

            if (best < 0)
                return false;

            image.X = free[best].X;
            image.Y = free[best].Y;
            Split(free, (image.X, image.Y, image.Width, image.Height));
        }

        return true;
    }

    /// <summary>Cuts a used rectangle out of every free one it overlaps, then drops free rectangles inside others.</summary>
    private static void Split(
        List<(int X, int Y, int W, int H)> free,
        (int X, int Y, int W, int H) used
    )
    {
        var next = new List<(int X, int Y, int W, int H)>();

        foreach (var f in free)
        {
            if (
                used.X >= f.X + f.W
                || used.X + used.W <= f.X
                || used.Y >= f.Y + f.H
                || used.Y + used.H <= f.Y
            )
            {
                next.Add(f);

                continue;
            }

            if (used.X > f.X)
                next.Add((f.X, f.Y, used.X - f.X, f.H));

            if (used.X + used.W < f.X + f.W)
                next.Add((used.X + used.W, f.Y, f.X + f.W - (used.X + used.W), f.H));

            if (used.Y > f.Y)
                next.Add((f.X, f.Y, f.W, used.Y - f.Y));

            if (used.Y + used.H < f.Y + f.H)
                next.Add((f.X, used.Y + used.H, f.W, f.Y + f.H - (used.Y + used.H)));
        }

        free.Clear();

        for (var i = 0; i < next.Count; i++)
        {
            var a = next[i];
            var contained = false;

            for (var j = 0; j < next.Count && !contained; j++)
            {
                if (i == j)
                    continue;

                var b = next[j];

                contained =
                    a.X >= b.X
                    && a.Y >= b.Y
                    && a.X + a.W <= b.X + b.W
                    && a.Y + a.H <= b.Y + b.H
                    && (a != b || i > j);
            }

            if (!contained)
                free.Add(a);
        }
    }

    private sealed record Trimmed(
        RgbaImage Source,
        int Left,
        int Top,
        int Width,
        int Height,
        bool IsTrimmed
    )
    {
        public int X { get; set; }

        public int Y { get; set; }
    }
}
