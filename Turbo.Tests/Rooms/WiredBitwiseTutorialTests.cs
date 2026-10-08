using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Wired;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The worked examples of the Wired Faculty tutorial "Intro to Bitwise Operations" (11/10/2025,
/// edited 15/05/2026), run through Change Variable Value's operations. Wired shifts logically:
/// a right shift brings in zeros, also for a negative value.
/// </summary>
public sealed class WiredBitwiseTutorialTests
{
    [Theory]
    [InlineData(WiredVariableOperationType.BitwiseAnd, 13, 10, 8)]
    [InlineData(WiredVariableOperationType.BitwiseAnd, 13, 110, 12)]
    [InlineData(WiredVariableOperationType.BitwiseOr, 13, 10, 15)]
    [InlineData(WiredVariableOperationType.BitwiseOr, 13, 110, 111)]
    [InlineData(WiredVariableOperationType.BitwiseXor, 13, 10, 7)]
    [InlineData(WiredVariableOperationType.BitwiseXor, 13, 110, 99)]
    [InlineData(WiredVariableOperationType.ShiftLeft, 13, 2, 52)]
    [InlineData(WiredVariableOperationType.ShiftLeft, 5, 3, 40)]
    [InlineData(WiredVariableOperationType.ShiftRight, 13, 2, 3)]
    [InlineData(WiredVariableOperationType.ShiftRight, 40, 3, 5)]
    [InlineData(WiredVariableOperationType.BitCount, 13, 0, 3)]
    [InlineData(WiredVariableOperationType.BitCount, 110, 0, 5)]
    [InlineData(WiredVariableOperationType.NextHighBitInclusive, 13, 1, 2)]
    [InlineData(WiredVariableOperationType.PreviousLowBitInclusive, 110, 3, 0)]
    [InlineData(WiredVariableOperationType.GetBit, 13, 2, 1)]
    [InlineData(WiredVariableOperationType.SetBit, 13, 2, 13)]
    [InlineData(WiredVariableOperationType.ClearBit, 13, 2, 9)]
    [InlineData(WiredVariableOperationType.ToggleBit, 2, 2, 6)]
    public void The_tutorials_examples(
        WiredVariableOperationType operation,
        long value,
        long operand,
        long expected
    ) => WiredVariableOperations.Apply(operation, value, operand).Should().Be(expected);

    [Fact]
    public void A_right_shift_brings_in_zeros_for_a_negative_value() =>
        WiredVariableOperations
            .Apply(WiredVariableOperationType.ShiftRight, -1, 60)
            .Should()
            .Be(15);

    [Fact]
    public void No_high_bit_to_find_is_minus_one() =>
        WiredVariableOperations
            .Apply(WiredVariableOperationType.NextHighBitInclusive, 13, 4)
            .Should()
            .Be(-1);
}
