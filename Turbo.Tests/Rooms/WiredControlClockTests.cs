using System.Diagnostics;
using FluentAssertions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Control clock" commands 3 and 4 are the editor's "Pause" and "Resume"
/// (<c>wiredfurni.params.clock_control.3</c> / <c>.4</c>): pausing holds the time, resuming runs on
/// from it. The clock is running at 10 seconds.
/// </summary>
public sealed class WiredControlClockTests
{
    private const int CLOCK = 30;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pause_holds_the_time_and_resume_runs_on_from_it()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;

        await clock.AdjustAsync(WiredOperatorType.Set, 20, Ct);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);

        await clock.ControlAsync((WiredClockControlType)3, Ct);

        clock.IsRunning.Should().BeFalse();
        clock.GetState().Should().Be(10);

        await clock.ControlAsync((WiredClockControlType)4, Ct);

        clock.IsRunning.Should().BeTrue();
        clock.GetState().Should().Be(10);
    }

    /// <summary>
    /// A counter's time is not a furni state change: Adjust Counter set a counter to 30 and
    /// "Furni state changes" picking it did not fire (official client capture). The clock
    /// trigger and the item's display still follow.
    /// </summary>
    [Fact]
    public async Task Moving_the_time_does_not_announce_a_state_change()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;
        var heard = new List<RoomEvent>();
        _room.Harness.Module<RoomEventModule>().Register(new Recorder(heard));

        await clock.AdjustAsync(WiredOperatorType.Set, 60, Ct);
        await clock.ControlAsync(WiredClockControlType.Reset, Ct);

        heard.OfType<RoomItemStateChangedEvent>().Should().BeEmpty();
        heard.OfType<WiredClockTickEvent>().Select(tick => tick.HalfSeconds).Should().Equal(60, 0);
        clock.GetState().Should().Be(0);
    }

    /// <summary>
    /// A running counter keeps time even when the room tick runs its timers late. Each half
    /// second used to be rescheduled 500 ms after the late run, so passes 120 ms apart made every
    /// half second last 600 ms (a local server's counter took ~1.1 s per second). Three seconds
    /// of passes now count six half seconds.
    /// </summary>
    [Fact]
    public async Task A_running_counter_keeps_time_when_the_room_tick_runs_late()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;
        var timers = _room.Harness.Module<RoomTimerSystem>();
        var started = Stopwatch.StartNew();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);

        while (started.ElapsedMilliseconds < 3_050)
        {
            await Task.Delay(120, Ct);
            await timers.ProcessTimersAsync(NowMs(), Ct);
        }

        clock.HalfSeconds.Should().BeGreaterThanOrEqualTo(6);
    }

    /// <summary>
    /// Official client capture 2026-10-16 (five Control Clock starts): the counter counts the
    /// room's 500 ms cycles from the start, so "1" shows on the second cycle after it, 500..1000
    /// ms later, and every second after on the same cycle. Ours showed "1" 1000 ms after the
    /// start, off the cycle. The test room's cycles fall on whole multiples of 500 ms.
    /// </summary>
    [Fact]
    public async Task A_started_counter_ticks_on_the_rooms_500_ms_cycle()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;
        var timers = _room.Harness.Module<RoomTimerSystem>();
        var startedAt = NowMs();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);

        var startReturnedAt = NowMs();

        var firstHalf = -1L;
        var firstSecond = -1L;

        for (var t = startedAt; t <= startReturnedAt + 1_600 && firstSecond < 0; t++)
        {
            await timers.ProcessTimersAsync(t, Ct);

            if (firstHalf < 0 && clock.HalfSeconds == 1)
                firstHalf = t;

            if (clock.HalfSeconds == 2)
                firstSecond = t;
        }

        (firstHalf % 500).Should().Be(0, "the first half second is the room's next cycle");
        firstSecond.Should().Be(firstHalf + 500);
        (firstSecond - startedAt).Should().BeGreaterThan(500);
        (firstSecond - startReturnedAt).Should().BeLessThanOrEqualTo(1_000);
    }

    [Fact]
    public async Task A_counter_keeps_the_cycle_when_the_room_pass_runs_more_than_500_ms_late()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;
        var timers = _room.Harness.Module<RoomTimerSystem>();
        var startedAt = NowMs();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);

        // A busy test host: the passes below replay times the real clock has already left behind.
        await Task.Delay(1_100, Ct);

        var firstHalf = -1L;
        var firstSecond = -1L;

        for (var t = startedAt; t <= startedAt + 2_000 && firstSecond < 0; t++)
        {
            await timers.ProcessTimersAsync(t, Ct);

            if (firstHalf < 0 && clock.HalfSeconds == 1)
                firstHalf = t;

            if (clock.HalfSeconds == 2)
                firstSecond = t;
        }

        (firstHalf % 500).Should().Be(0);
        firstSecond.Should().Be(firstHalf + 500, "each half second is due on its boundary, not when the pass ran");
    }

    private static long NowMs() => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    private sealed class Recorder(List<RoomEvent> heard) : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            heard.Add(evt);

            return Task.CompletedTask;
        }
    }
}
