using System;

namespace Turbo.Rooms.Systems;

/// <summary>Builds the 32 bit shore mask read by <c>FurnitureWaterAreaVisualization</c>.</summary>
public static class WaterAreaMask
{
    public static int Build(int width, int length, Func<int, int, bool> isWater)
    {
        var ringCount = (width + 2) * 2 + (length * 2);
        if (width < 1 || length < 1 || ringCount > 32)
            throw new ArgumentOutOfRangeException(nameof(width));

        var mask = 0;
        var bit = 0;

        for (var x = width + 1; x >= 0; x--)
            SetIfWater(ref mask, ref bit, isWater, x - 1, length);

        for (var y = length - 1; y >= 0; y--)
        {
            SetIfWater(ref mask, ref bit, isWater, width, y);
            SetIfWater(ref mask, ref bit, isWater, -1, y);
        }

        for (var x = width + 1; x >= 0; x--)
            SetIfWater(ref mask, ref bit, isWater, x - 1, -1);

        return mask;
    }

    private static void SetIfWater(
        ref int mask,
        ref int bit,
        Func<int, int, bool> isWater,
        int x,
        int y
    )
    {
        if (isWater(x, y))
            mask |= 1 << bit;

        bit++;
    }
}
