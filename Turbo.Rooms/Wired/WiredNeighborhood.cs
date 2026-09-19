using System.Collections.Generic;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

/// <summary>
/// Decodes the painted neighbourhood of the "in neighbourhood" selectors: the client stores a
/// bit per tile of a (2r+1)² square, ordered along a spiral from the centre, packed into ints.
/// </summary>
public static class WiredNeighborhood
{
    /// <summary>The room tile ids the mask covers around a start tile, offset by the root tile.</summary>
    public static IEnumerable<int> Tiles(
        RoomGrain roomGrain,
        int startX,
        int startY,
        int rootX,
        int rootY,
        IReadOnlyList<int> masks
    )
    {
        var radius = roomGrain._roomConfig.WiredNeighborhoodRadius;
        var width = roomGrain.MapModule.Width;
        var height = roomGrain.MapModule.Height;
        var size = radius * 2 + 1;
        var totalBits = size * size;
        var mask = IntParamsToBoolMask(masks, totalBits);

        foreach (var entry in WalkSpiral(radius))
        {
            if (!mask[entry.Rank])
                continue;

            var x = startX + entry.Dx - rootX;
            var y = startY - entry.Dy - rootY;

            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                continue;

            yield return (y * width) + x;
        }
    }

    private readonly record struct SpiralEntry(short Dx, short Dy, int Rank);

    private static List<SpiralEntry> WalkSpiral(int radius)
    {
        var size = radius * 2 + 1;
        var total = size * size;
        var result = new List<SpiralEntry>(total);
        var x = 0;
        var y = 0;
        var rank = 0;

        result.Add(new SpiralEntry(0, 0, rank++));

        if (total <= 1)
            return result;

        var stepLen = 1;

        while (rank < total)
        {
            for (var i = 0; i < stepLen && rank < total; i++)
            {
                x++;
                result.Add(new((short)x, (short)y, rank++));
            }

            for (var i = 0; i < stepLen && rank < total; i++)
            {
                y++;
                result.Add(new((short)x, (short)y, rank++));
            }

            stepLen++;

            for (var i = 0; i < stepLen && rank < total; i++)
            {
                x--;
                result.Add(new((short)x, (short)y, rank++));
            }

            for (var i = 0; i < stepLen && rank < total; i++)
            {
                y--;
                result.Add(new((short)x, (short)y, rank++));
            }

            stepLen++;
        }

        return result;
    }

    private static bool[] IntParamsToBoolMask(IReadOnlyList<int> intParams, int totalBits)
    {
        var mask = new bool[totalBits];
        var bitIndex = 0;

        for (var i = 0; i < intParams.Count && bitIndex < totalBits; i++)
        {
            var v = unchecked((uint)intParams[i]);

            for (var shift = 0; shift < 32 && bitIndex < totalBits; shift += 8)
                bitIndex = UnpackByte((byte)((v >> shift) & 0xFF), mask, bitIndex, totalBits);
        }

        return mask;
    }

    private static int UnpackByte(byte b, bool[] mask, int bitIndex, int totalBits)
    {
        for (var bit = 0; bit < 8 && bitIndex < totalBits; bit++)
            mask[bitIndex++] = ((b >> bit) & 1) != 0;

        return bitIndex;
    }
}
