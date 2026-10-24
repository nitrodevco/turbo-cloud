using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// What each "Change Variable Value" operation makes of a value and its operand. All 64 bits are
/// kept and an overflow wraps, as Java's long arithmetic does: 2^63-1 plus 1 is -2^63 and 2 to
/// the power 64 is 0; -5 / 2 is -2 and -7 % 3 is -1 (official client capture 2026-10-24,
/// WIN63-202610201032, wired matrix action.41.B4d/B4e/B4h).
/// </summary>
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
            WiredVariableOperationType.Divide => operand == 0
                ? DivideByZero(current)
                : current / operand,
            // x % 0 is 0, no error (official client capture 2026-10-24, action.41.B4g).
            WiredVariableOperationType.Modulo => operand == 0 ? 0 : current % operand,
            WiredVariableOperationType.Power => Power(current, operand),
            // "Set minimum" makes the operand a floor, "Set maximum" a ceiling (official client capture).
            WiredVariableOperationType.SetMinimum => Math.Max(current, operand),
            WiredVariableOperationType.SetMaximum => Math.Min(current, operand),
            WiredVariableOperationType.Random => operand <= 0
                ? 0
                : Random.Shared.NextInt64(0, operand + 1),
            WiredVariableOperationType.Absolute => Math.Abs(current),
            WiredVariableOperationType.BitwiseAnd => current & operand,
            WiredVariableOperationType.BitwiseOr => current | operand,
            WiredVariableOperationType.BitwiseXor => current ^ operand,
            WiredVariableOperationType.Invert => ~current,
            WiredVariableOperationType.ShiftLeft => current << (int)Math.Clamp(operand, 0, 63),
            // A logical shift, as wired's is ("Intro to Bitwise Operations"): zeros come in on the left.
            WiredVariableOperationType.ShiftRight => current >>> (int)Math.Clamp(operand, 0, 63),
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

    /// <summary>
    /// A non-negative power by repeated multiplication, wrapping where it overflows: 2^40 is kept
    /// whole, 2^63 is -2^63 and 2^64 is 0 (official client capture 2026-10-24, action.41.B4c/B4e).
    /// A negative exponent is 1 (not measured).
    /// </summary>
    private static long Power(long value, long exponent)
    {
        var result = 1L;

        while (exponent > 0)
        {
            if ((exponent & 1) == 1)
                result = unchecked(result * value);

            value = unchecked(value * value);
            exponent >>= 1;
        }

        return result;
    }

    /// <summary>
    /// 10 / 0 reads 2147483647 and stays past 2^51 after a further / 2^20, so it is the largest
    /// long (official client capture 2026-10-24, action.41.B4f, 3/3, no error); -10 / 0 is the
    /// smallest, still the smallest after / 2^32, and 0 / 0 is 0 (action.41.R11).
    /// </summary>
    private static long DivideByZero(long value) =>
        value switch
        {
            > 0 => long.MaxValue,
            < 0 => long.MinValue,
            _ => 0,
        };

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
