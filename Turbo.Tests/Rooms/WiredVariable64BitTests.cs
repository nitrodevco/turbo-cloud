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
/// Bitwise Operations"); the client is sent their low 32 bits. <c>@current_time</c> is unix
/// milliseconds, past what 32 bits hold.
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

    [Fact]
    public void The_client_is_sent_the_low_32_bits()
    {
        new WiredVariableValue((1L << 32) + 5).ToClient().Should().Be(5);
    }

    [Fact]
    public void Current_time_is_unix_milliseconds()
    {
        var room = new WiredRoom();
        var variable = new RoomCurrentTimeVariable(room.Harness.Room);
        var snapshot = variable.GetVarSnapshot();
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, 0),
                out var value
            )
            .Should()
            .BeTrue();

        ((long)value).Should().BeInRange(before, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        snapshot.VariableName.Should().Be("@current_time");
    }
}
