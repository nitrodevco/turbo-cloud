using System;
using FluentAssertions;
using Xunit;

namespace Docs.Patterns;

// Reference-only sample: prioritize failure-path coverage for generated logic.
public static class PresenceRule
{
    public static bool CanEnterRoom(int roomId, bool isBanned)
    {
        if (roomId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roomId));
        }

        return !isBanned;
    }
}

public class PresenceRuleTests
{
    [Theory]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public void CanEnterRoom_ReturnsExpected(int roomId, bool isBanned, bool expected)
    {
        var result = PresenceRule.CanEnterRoom(roomId, isBanned);
        result.Should().Be(expected);
    }

    [Fact]
    public void CanEnterRoom_ThrowsForInvalidRoomId()
    {
        var action = () => PresenceRule.CanEnterRoom(0, false);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
