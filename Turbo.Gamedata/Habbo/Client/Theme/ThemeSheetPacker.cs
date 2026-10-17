using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Assets.Conversion;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// The theme's sprites on one sheet, as Studio's <c>packSprites(..., { extrude: true })</c> lays
/// them out (<c>server/sprite-packer.ts</c>): each sprite whole, in a cell with a one-pixel gutter
/// of its own edge pixels round it, cells side by side on shelves about as wide as the sheet will be
/// tall, tallest first. The gutter is what a nine-slice or a tile stretched well past its size
/// samples; without it the client blends in the neighbour's pixels.
/// </summary>
internal static class ThemeSheetPacker
{
    /// <summary>The widest a shelf is made, unless one sprite is wider.</summary>
    private const int MAX_ROW_WIDTH = 2048;

    /// <summary>The gutter round each sprite, in pixels.</summary>
    private const int INSET = 1;

    /// <summary>The sheet, and its Pixi spritesheet manifest naming each sprite's rect.</summary>
    public static (RgbaImage Sheet, JsonObject Spritesheet) Pack(
        string sheetName,
        IReadOnlyDictionary<string, RgbaImage> sprites
    )
    {
        var images = sprites
            .OrderByDescending(x => x.Value.Height)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        var area = images.Sum(x =>
            (long)(x.Value.Width + (INSET * 2)) * (x.Value.Height + (INSET * 2))
        );
        var rowWidth = Math.Max(
            images.Max(x => x.Value.Width + (INSET * 2)),
            (int)Math.Min(MAX_ROW_WIDTH, Math.Ceiling(Math.Sqrt(area) * 1.15))
        );
        var placed = new List<(string Name, RgbaImage Image, int X, int Y)>();
        int x = 0,
            y = 0,
            rowHeight = 0,
            width = 1;

        foreach (var (name, image) in images)
        {
            var cellWidth = image.Width + (INSET * 2);

            if (x > 0 && x + cellWidth > rowWidth)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            placed.Add((name, image, x + INSET, y + INSET));
            x += cellWidth;
            rowHeight = Math.Max(rowHeight, image.Height + (INSET * 2));
            width = Math.Max(width, x);
        }

        var height = Math.Max(1, y + rowHeight);
        var sheet = new RgbaImage(width, height, new byte[width * height * 4]);
        var frames = new JsonObject();

        foreach (var (name, image, at, top) in placed)
        {
            ThemeCanvas.Blit(image, sheet, at, top);
            ThemeCanvas.ExtrudeEdges(sheet, at, top, image.Width, image.Height);

            frames[name] = new JsonObject
            {
                ["frame"] = new JsonObject
                {
                    ["x"] = at,
                    ["y"] = top,
                    ["w"] = image.Width,
                    ["h"] = image.Height,
                },
                ["rotated"] = false,
                ["trimmed"] = false,
                ["spriteSourceSize"] = new JsonObject
                {
                    ["x"] = 0,
                    ["y"] = 0,
                    ["w"] = image.Width,
                    ["h"] = image.Height,
                },
                ["sourceSize"] = new JsonObject { ["w"] = image.Width, ["h"] = image.Height },
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

        return (sheet, spritesheet);
    }
}
