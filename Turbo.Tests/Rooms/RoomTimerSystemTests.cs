using FluentAssertions;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Systems;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A timer that schedules its next run from its callback (a clock's tick) waits for the next
/// pass, even when the next run is already due: one pass of the room tick runs it once.
/// </summary>
public sealed class RoomTimerSystemTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_timer_scheduled_by_its_own_callback_runs_in_the_next_pass()
    {
        var room = new RoomHarness();
        var timers = room.Module<RoomTimerSystem>();
        var runs = 0;

        Task Tick(CancellationToken ct)
        {
            // Bounded, so the test ends even when a pass keeps running what it just scheduled.
            if (++runs < 100)
                timers.Schedule(1, 0, Tick);

            return Task.CompletedTask;
        }

        timers.Schedule(1, 0, Tick);

        await timers.ProcessTimersAsync(NowMs(room) + 1_000, Ct);
        runs.Should().Be(1);

        await timers.ProcessTimersAsync(NowMs(room) + 1_000, Ct);
        runs.Should().Be(2);
    }

    private static long NowMs(RoomHarness room) =>
        (long)
            typeof(RoomGrain)
                .GetMethod(
                    "NowMs",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(room.Room, null)!;
}
