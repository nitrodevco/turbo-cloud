using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Mapping;

public static class RoomModelCompiler
{
    public static string CleanModelString(string model) =>
        model.Trim().ToLower().Replace("\r\n", "\r").Replace("\n", "\r");

    public static CompiledRoomModelSnapshot CompileModelFromString(string model)
    {
        var rows = SplitLines(model);

        if (rows.Count == 0)
            throw new InvalidDataException("Room model data is empty.");

        var height = rows.Count;
        var width = rows.Max(x => x.Length);
        var size = width * height;
        var heights = new double[size];
        var states = new byte[size];

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];

            for (var x = 0; x < width; x++)
            {
                int idx = y * width + x;
                char ch = (x < row.Length) ? row[x] : 'x';

                if (ch.Equals('x'))
                {
                    heights[idx] = 0.0;
                    states[idx] = (byte)RoomTileStateType.Closed;
                }
                else
                {
                    var heightIndex = "abcdefghijklmnopqrstuvwxyz".IndexOf(ch);
                    var tileHeight =
                        heightIndex == -1 ? int.Parse(ch.ToString()) : heightIndex + 10;

                    heights[idx] = tileHeight;
                    states[idx] = (byte)RoomTileStateType.Open;
                }
            }
        }

        return new CompiledRoomModelSnapshot(width, height, heights, states);
    }

    public static short EncodeHeight(double height, bool stackingBlocked)
    {
        if (height < 0)
            return -1;

        int stackingMask = 1 << 14;
        int heightMask = stackingMask - 1;
        int raw = (int)Math.Round(height * 256.0);

        if (raw < 0)
            raw = 0;

        if (raw > heightMask)
            raw = heightMask;

        int value = raw | (stackingBlocked ? stackingMask : 0);

        value &= 0x7FFF;

        return unchecked((short)value);
    }

    private static List<string> SplitLines(string s)
    {
        var lines = new List<string>();

        using var sr = new StringReader(s);

        string? line;

        while ((line = sr.ReadLine()) is not null)
            lines.Add(line);

        return lines;
    }
}
