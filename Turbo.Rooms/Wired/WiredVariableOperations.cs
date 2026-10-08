using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>What each "Change Variable Value" operation makes of a value and its operand.</summary>
public static class WiredVariableOperations
{
    public static long Apply(WiredVariableOperationType operation, long current, long operand)
    {
        long result = operation switch
        {
            WiredVariableOperationType.Set => operand,
            WiredVariableOperationType.Add => current + operand,
            WiredVariableOperationType.Subtract => current - operand,
            WiredVariableOperationType.Multiply => current * operand,
            WiredVariableOperationType.Divide => operand == 0 ? current : current / operand,
            WiredVariableOperationType.Modulo => operand == 0 ? current : current % operand,
            WiredVariableOperationType.Power => Power(current, operand),
            WiredVariableOperationType.Minimum => Math.Min(current, operand),
            WiredVariableOperationType.Maximum => Math.Max(current, operand),
            WiredVariableOperationType.Random => operand <= 0
                ? 0
                : Random.Shared.NextInt64(0, operand + 1),
            WiredVariableOperationType.Absolute => Math.Abs(current),
            WiredVariableOperationType.BitwiseAnd => current & operand,
            WiredVariableOperationType.BitwiseOr => current | operand,
            WiredVariableOperationType.BitwiseXor => current ^ operand,
            WiredVariableOperationType.Invert => ~current,
            WiredVariableOperationType.ShiftLeft => current << (int)Math.Clamp(operand, 0, 63),
            WiredVariableOperationType.ShiftRight => current >> (int)Math.Clamp(operand, 0, 63),
            WiredVariableOperationType.BitCount => long.PopCount(current),
            WiredVariableOperationType.NextLowBitInclusive => FindBit(current, operand, false, 1),
            WiredVariableOperationType.NextHighBitInclusive => FindBit(current, operand, true, 1),
            WiredVariableOperationType.PreviousLowBitInclusive => FindBit(
                current,
                operand,
                false,
                -1
            ),
            WiredVariableOperationType.PreviousHighBitInclusive => FindBit(
                current,
                operand,
                true,
                -1
            ),
            WiredVariableOperationType.NextLowBitExclusive => FindBit(
                current,
                operand + 1,
                false,
                1
            ),
            WiredVariableOperationType.NextHighBitExclusive => FindBit(
                current,
                operand + 1,
                true,
                1
            ),
            WiredVariableOperationType.PreviousLowBitExclusive => FindBit(
                current,
                operand - 1,
                false,
                -1
            ),
            WiredVariableOperationType.PreviousHighBitExclusive => FindBit(
                current,
                operand - 1,
                true,
                -1
            ),
            WiredVariableOperationType.GetBit => IsBitPosition(operand)
                ? (current >> (int)operand) & 1
                : 0,
            WiredVariableOperationType.SetBit => IsBitPosition(operand)
                ? current | (1L << (int)operand)
                : current,
            WiredVariableOperationType.ClearBit => IsBitPosition(operand)
                ? current & ~(1L << (int)operand)
                : current,
            WiredVariableOperationType.ToggleBit => IsBitPosition(operand)
                ? current ^ (1L << (int)operand)
                : current,
            _ => current,
        };

        return result;
    }

    /// <summary>A non-negative power, held at the 64-bit limits where it would overflow.</summary>
    private static long Power(long value, long exponent)
    {
        if (exponent <= 0)
            return 1;

        var result = Math.Pow(value, exponent);

        if (result >= long.MaxValue)
            return long.MaxValue;
        if (result <= long.MinValue)
            return long.MinValue;

        return (long)result;
    }

    private static bool IsBitPosition(long position) => position is >= 0 and < 64;

    /// <summary>
    /// The position of the first bit that is set (high) or clear (low), looking from
    /// <paramref name="from"/> towards the higher bits (step 1) or the lower ones (step -1); -1 when
    /// there is none (the "Intro to Bitwise Operations" tutorial, Wired Faculty).
    /// </summary>
    private static long FindBit(long value, long from, bool high, int step)
    {
        for (var position = from; IsBitPosition(position); position += step)
        {
            if ((((value >> (int)position) & 1) == 1) == high)
                return position;
        }

        return -1;
    }
}
