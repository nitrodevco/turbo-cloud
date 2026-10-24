using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables.Room;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Variable values are 64-bit signed integers, as Habbo keeps them (Wired Faculty, "Intro to
/// Bitwise Operations"); the client is sent them held within 32 bits. <c>@current_time</c> is
/// unix milliseconds, past what 32 bits hold.
/// </summary>
public sealed class WiredVariable64BitTests
{
    [Fact]
    public void Operations_keep_all_64_bits()
    {
        WiredVariableOperations
            .Apply(WiredVariableOperationType.ShiftLeft, 13, 40)
            .Should()
            .Be(13L << 40);
        WiredVariableOperations
            .Apply(WiredVariableOperationType.GetBit, 1L << 50, 50)
            .Should()
            .Be(1);
        WiredVariableOperations
            .Apply(WiredVariableOperationType.Multiply, 3_000_000_000, 2)
            .Should()
            .Be(6_000_000_000);
    }

    /// <summary>
    /// Change Variable on a room variable, read back with WiredGetVariablesForObject after each
    /// firing (official client capture 2026-10-24, WIN63-202610201032, explore 17-38-53, 17-39-58,
    /// 17-41-06): each row is the lab's sequence, "op operand = readout".
    /// </summary>
    [Theory]
    // action.41.B4: stored as 64 bits, read clamped: MAX + 1 - 1 is MAX, not MAX - 1 (2/2).
    [InlineData("set 2147483647 = 2147483647; add 1 = 2147483647; sub 1 = 2147483647")]
    // action.41.B4b: the smallest int, minus 1, still reads the smallest (1/1).
    [InlineData("set -2147483648 = -2147483648; sub 1 = -2147483648")]
    // action.41.B4c: 2^40 reads MAX, divided by 2^20 it is 2^20: the whole 2^40 was kept (1/1).
    [InlineData("set 2 = 2; pow 40 = 2147483647; div 1048576 = 1048576")]
    // action.41.B4d: 2^32 reads MAX; 2^63-1 plus 1 wraps to -2^63 (1/1).
    [InlineData("set 4294967296 = 2147483647")]
    [InlineData("set 9223372036854775807 = 2147483647; add 1 = -2147483648")]
    // action.41.B4e: 2^63 is -2^63, 2^64 wraps to 0 (2^64 2/2, 2^63 1/1).
    [InlineData("set 2 = 2; pow 63 = -2147483648")]
    [InlineData("set 2 = 2; pow 64 = 0")]
    // action.41.B4f: x / 0 is past 2^51 and no error (3/3).
    [InlineData("set 10 = 10; div 0 = 2147483647; sub 1 = 2147483647; div 1048576 = 2147483647")]
    // action.41.B4g: x % 0 is 0 (1/1).
    [InlineData("set 10 = 10; mod 0 = 0")]
    // action.41.B4h: a division truncates toward zero, a remainder keeps the dividend's sign.
    [InlineData("set -5 = -5; div 2 = -2")]
    [InlineData("set -7 = -7; mod 3 = -1")]
    public void Change_variable_arithmetic_reads_as_habbo_measured_it(string steps)
    {
        long value = 0;

        foreach (var step in steps.Split(';', StringSplitOptions.TrimEntries))
        {
            var (operation, rest) = (step.Split(' ')[0], step.Split(' ', 2)[1].Split('='));
            var operand = long.Parse(rest[0].Trim());

            value = WiredVariableOperations.Apply(
                operation switch
                {
                    "set" => WiredVariableOperationType.Set,
                    "add" => WiredVariableOperationType.Add,
                    "sub" => WiredVariableOperationType.Subtract,
                    "div" => WiredVariableOperationType.Divide,
                    "mod" => WiredVariableOperationType.Modulo,
                    "pow" => WiredVariableOperationType.Power,
                    _ => throw new ArgumentException(step),
                },
                value,
                operand
            );

            new WiredVariableValue(value).ToClient().Should().Be(int.Parse(rest[1].Trim()), step);
        }
    }

    [Fact]
    public void Current_time_is_unix_milliseconds()
    {
        var room = new WiredRoom();
        var variable = new RoomCurrentTimeVariable(room.Harness.Room);
        var snapshot = variable.GetVarSnapshot();

        variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, 0),
                out var value
            )
            .Should()
            .BeTrue();

        ((long)value).Should().Be(room.Clock.GetUtcNow().ToUnixTimeMilliseconds());
        snapshot.VariableName.Should().Be("@current_time");
    }
}
