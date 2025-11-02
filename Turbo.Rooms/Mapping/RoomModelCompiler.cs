using System.Collections.Generic;
using System.IO;
using System.Linq;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Rooms.Mapping;

public static class RoomModelCompiler
{
    public static CompiledRoomModelSnapshot CompileModelFromEntity(RoomModelEntity entity)
    {
        var model = entity.Model;
        var rows = SplitLines(model);

        if (rows.Count == 0)
            throw new InvalidDataException("Room model data is empty.");

        var height = (short)rows.Count;
        var width = (short)rows.Max(x => x.Length);
        var heights = new byte[width * height];
        var states = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];

            for (var x = 0; x < width; x++)
            {
                int idx = y * width + x;
                char ch = (x < row.Length) ? row[x] : 'x';

                if (ch.Equals('x'))
                {
                    heights[idx] = 0;
                    states[idx] = (byte)RoomTileType.Closed;
                }
                else
                {
                    var heightIndex = "abcdefghijklmnopqrstuvwxyz".IndexOf(ch);
                    var tileHeight =
                        heightIndex == -1 ? int.Parse(ch.ToString()) : heightIndex + 10;

                    heights[idx] = (byte)tileHeight;
                    states[idx] = (byte)RoomTileType.Open;
                }
            }
        }

        return new CompiledRoomModelSnapshot(entity.Id, width, height, heights, states);
    }

    private static List<string> SplitLines(string s)
    {
        var lines = new List<string>();

        using var sr = new StringReader(s);

        string? line;

        while ((line = sr.ReadLine()) is not null)
            lines.Add(line.TrimEnd().ToLower());

        return lines;
    }
}
