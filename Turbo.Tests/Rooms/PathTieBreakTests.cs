using FluentAssertions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Among equally short routes Habbo walks the diagonal steps first and the straight ones last
/// (recorded on hhus, 2026-10-12, in a starter room): (5,6) to (8,8) goes 6,7 / 7,8 / 8,8 and
/// (8,8) to (6,3) goes 7,7 / 6,6 / 6,5 / 6,4 / 6,3. Turbo's queue broke the tie the other way
/// round (6,6 / 7,7 / 8,8).
/// </summary>
public sealed class PathTieBreakTests
{
    [Theory]
    [InlineData(5, 6, 8, 8, "6,7 7,8 8,8")]
    [InlineData(8, 8, 6, 3, "7,7 6,6 6,5 6,4 6,3")]
    [InlineData(10, 9, 5, 6, "9,8 8,7 7,6 6,6 5,6")]
    public void Diagonal_steps_come_first_among_equal_routes(
        int fromX,
        int fromY,
        int toX,
        int toY,
        string expected
    )
    {
        var room = new WiredRoom(12, 12);
        var avatar = room.Enter(1, fromX, fromY);
        var path = new List<int>();

        room.Harness.Room.PathingSystem.TryFindPath(
                avatar,
                fromY * 12 + fromX,
                toY * 12 + toX,
                path
            )
            .Should()
            .BeTrue();

        // The path runs from the goal back to the first step.
        string.Join(' ', Enumerable.Reverse(path).Select(i => $"{i % 12},{i / 12}"))
            .Should()
            .Be(expected);
    }
}
