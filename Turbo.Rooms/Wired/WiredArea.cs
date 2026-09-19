using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The rectangle an area selector covers, as the client sends it (root x, root y, width,
/// height). The numbers come from the client, so the rectangle is always cut to the map and
/// to <see cref="WiredConfig.SelectorMaxAreaSize"/> tiles, counted row by row from the
/// root, before anything walks it.
/// </summary>
internal readonly record struct WiredArea(int X, int Y, int Width, int Height, int MaxTiles)
{
    /// <summary>Int param rules for the four values, in wire order.</summary>
    public static List<IWiredParamRule> GetParamRules(WiredConfig config) =>
        [
            new WiredRangeParamRule(0, config.MaxCoordinate, 0),
            new WiredRangeParamRule(0, config.MaxCoordinate, 0),
            new WiredRangeParamRule(0, config.MaxCoordinate, 0),
            new WiredRangeParamRule(0, config.MaxCoordinate, 0),
        ];

    public static WiredArea Create(RoomGrain roomGrain, int x, int y, int width, int height)
    {
        var mapWidth = roomGrain.MapModule.Width;
        var mapHeight = roomGrain.MapModule.Height;

        x = Math.Clamp(x, 0, mapWidth);
        y = Math.Clamp(y, 0, mapHeight);
        width = Math.Clamp(width, 0, mapWidth - x);
        height = Math.Clamp(height, 0, mapHeight - y);

        return new WiredArea(
            x,
            y,
            width,
            height,
            Math.Max(0, roomGrain._wiredConfig.SelectorMaxAreaSize)
        );
    }

    public bool Contains(int x, int y)
    {
        if (x < X || y < Y || x >= X + Width || y >= Y + Height)
            return false;

        return ((y - Y) * Width) + (x - X) < MaxTiles;
    }

    /// <param name="mapWidth">Tile ids are row-major over the whole map.</param>
    public IEnumerable<int> GetTileIds(int mapWidth)
    {
        var count = 0;

        for (var dy = 0; dy < Height; dy++)
        {
            for (var dx = 0; dx < Width; dx++)
            {
                if (count++ >= MaxTiles)
                    yield break;

                yield return ((Y + dy) * mapWidth) + X + dx;
            }
        }
    }
}
