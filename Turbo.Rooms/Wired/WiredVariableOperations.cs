using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>What each "Change Variable Value" operation makes of a value and its operand.</summary>
public static class WiredVariableOperations
{
    public static int Apply(WiredVariableOperationType operation, long current, long operand)
    {
        long result = operation switch
        {
            WiredVariableOperationType.Set => operand,
            WiredVariableOperationType.Add => current + operand,
            WiredVariableOperationType.Subtract => current - operand,
            WiredVariableOperationType.Multiply => current * operand,
            WiredVariableOperationType.Divide => operand == 0 ? current : current / operand,
            WiredVariableOperationType.Modulo => operand == 0 ? current : current % operand,
            WiredVariableOperationType.Power => (long)Math.Pow(current, Math.Clamp(operand, 0, 31)),
            WiredVariableOperationType.Minimum => Math.Min(current, operand),
            WiredVariableOperationType.Maximum => Math.Max(current, operand),
            WiredVariableOperationType.Random => operand <= 0
                ? 0
                : Random.Shared.NextInt64(0, operand + 1),
            WiredVariableOperationType.Negate => -current,
            WiredVariableOperationType.BitwiseAnd => current & operand,
            WiredVariableOperationType.BitwiseOr => current | operand,
            WiredVariableOperationType.BitwiseXor => current ^ operand,
            WiredVariableOperationType.Invert => ~current,
            WiredVariableOperationType.ShiftLeft => current << (int)Math.Clamp(operand, 0, 31),
            WiredVariableOperationType.ShiftRight => current >> (int)Math.Clamp(operand, 0, 31),
            WiredVariableOperationType.Absolute => Math.Abs(current),
            WiredVariableOperationType.IsEqual => current == operand ? 1 : 0,
            WiredVariableOperationType.IsNotEqual => current != operand ? 1 : 0,
            WiredVariableOperationType.IsLess => current < operand ? 1 : 0,
            WiredVariableOperationType.IsGreater => current > operand ? 1 : 0,
            WiredVariableOperationType.SetIfLess => current < operand ? operand : current,
            WiredVariableOperationType.SetIfGreater => current > operand ? operand : current,
            WiredVariableOperationType.AddClampedToOperand => Math.Min(current + 1, operand),
            WiredVariableOperationType.SubtractClampedToOperand => Math.Max(current - 1, operand),
            WiredVariableOperationType.Average => (current + operand) / 2,
            WiredVariableOperationType.Distance => Math.Abs(current - operand),
            WiredVariableOperationType.IsLessOrEqual => current <= operand ? 1 : 0,
            WiredVariableOperationType.IsGreaterOrEqual => current >= operand ? 1 : 0,
            _ => current,
        };

        return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
    }
}
