using System.Collections.Generic;
using System.IO;
using System.Linq;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Rooms.Mapping;

public static class RoomModelCompiler
{
    public static CompiledRoomModelSnapshot CompileModelFromString(string model)
    {
        var rows = SplitLines(model);

        if (rows.Count == 0)
            throw new InvalidDataException("Room model data is empty.");

        var height = rows.Count;
        var width = rows.Max(x => x.Length);
        var size = width * height;
        var heights = new float[size];
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
                    heights[idx] = 0.0f;
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
