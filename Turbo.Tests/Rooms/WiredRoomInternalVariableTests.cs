using System.Diagnostics;
using FluentAssertions;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Room;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room's own internal variables builders read off the global list: <c>@user_count</c>
/// follows the players in the room and <c>@wired_timer</c> reads the timer "reset timers" resets.
/// </summary>
public class WiredRoomInternalVariableTests
{
    [Fact]
    public void User_count_is_the_players_in_the_room()
    {
        var room = new WiredRoom();
        var variable = new RoomUserCountVariable(room.Harness.Room);

        Read(variable).Should().Be(0);

        room.Enter(1, 1, 1);
        room.Enter(2, 2, 2);

        Read(variable).Should().Be(2);
    }

    [Fact]
    public void Wired_timer_counts_half_second_pulses_and_restarts_on_reset()
    {
        var room = new WiredRoom();
        var variable = new RoomWiredTimerVariable(room.Harness.Room);
        var wired = room.Harness.Room.WiredSystem;
        var now = NowMs();

        // Ten seconds ago, so the clock has been running: twenty pulses.
        wired.ResetTimers(now - 10_000);
        Read(variable).Should().BeInRange(20, 21);

        wired.ResetTimers(NowMs());
        Read(variable).Should().Be(0);
    }

    [Fact]
    public void Both_are_listed_after_furni_count_as_the_official_list_has_them()
    {
        var room = new WiredRoom();
        var ids = new WiredInternalVariable[]
        {
            new RoomFurniCountVariable(room.Harness.Room),
            new RoomUserCountVariable(room.Harness.Room),
            new RoomWiredTimerVariable(room.Harness.Room),
        }
            .Select(x => x.GetVarSnapshot().VariableId)
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
        ids.Should().BeInDescendingOrder();
    }

    /// <summary>The room's clock (<c>RoomGrain.NowMs</c>, internal there).</summary>
    private static long NowMs() => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    private static int Read(WiredInternalVariable variable)
    {
        var snapshot = variable.GetVarSnapshot();

        variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return (int)value;
    }
}
