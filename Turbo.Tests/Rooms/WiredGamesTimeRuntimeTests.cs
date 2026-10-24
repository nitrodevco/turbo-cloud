using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The games and time boxes running, against what was measured on the official server
/// (official client capture 2026-10-03): the clock's start, stop, reset, pause and resume, and the
/// long and short repeaters firing until they leave their stack. Case ids are category.code.case.
/// </summary>
public sealed class WiredGamesTimeRuntimeTests
{
    private const int CLOCK = 30;
    private const int PAD = 21;
    private const int TRIGGER = 1;
    private const int EFFECT = 3;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    /// <summary>
    /// action.28.R7: a started counter stopped at 3 shows 3; reset after stopping shows 0; paused
    /// at 2 it holds 2; resumed and then stopped it shows 5.
    /// </summary>
    [Fact]
    public async Task The_clock_starts_stops_resets_pauses_and_resumes_as_measured_action_28_R7()
    {
        var clock = AddClock();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await RunClockUntilAsync(clock, 3);
        await clock.ControlAsync(WiredClockControlType.Stop, Ct);
        await RunClockAsync(2_000);
        clock.GetState().Should().Be(3, "a stopped clock keeps its time");

        await clock.ControlAsync(WiredClockControlType.Reset, Ct);
        clock.GetState().Should().Be(0, "reset after stopping");

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await RunClockUntilAsync(clock, 2);
        await clock.ControlAsync(WiredClockControlType.Pause, Ct);
        await RunClockAsync(2_000);
        clock.GetState().Should().Be(2, "a paused clock holds its time");

        await clock.ControlAsync(WiredClockControlType.Resume, Ct);
        await RunClockUntilAsync(clock, 5);
        await clock.ControlAsync(WiredClockControlType.Stop, Ct);
        await RunClockAsync(2_000);
        clock.GetState().Should().Be(5, "resumed from 2 and stopped");
    }

    /// <summary>
    /// Measured (official client capture 2026-10-03, a counter, Adjust Clock, Clock Time Matches):
    /// set to 30 s, "time = 30 s" holds and "time = 20 s" does not; started, after 2.2 s
    /// "time &gt; 1 s" holds (action.38 operation 2, condition.35 comparison 1 and 2).
    /// </summary>
    [Fact]
    public async Task Adjust_sets_the_time_clock_time_matches_compares_measured_clocks()
    {
        var clock = AddClock();
        var matches = _room.AddBox<WiredConditionCounterTimeMatch>(
            4,
            0,
            0,
            "wf_cnd_counter_time_matches"
        );
        var adjust = _room.AddBox<WiredActionAdjustClock>(5, 1, 1, "wf_act_adjust_clock");

        await SaveClockBoxAsync<UpdateActionMessage>(5, [30, 0, 0, (int)WiredOperatorType.Set]);
        await adjust.LoadWiredAsync(Ct);
        await adjust.ExecuteAsync(new WiredExecutionContext(_room.Harness.Room), Ct);

        await SaveClockBoxAsync<UpdateConditionMessage>(4, [30, 0, 0, 1]);
        matches.Evaluate(Context()).Should().BeTrue("set to 30 s");
        await SaveClockBoxAsync<UpdateConditionMessage>(4, [20, 0, 0, 1]);
        matches.Evaluate(Context()).Should().BeFalse("30 s is not 20 s");

        await clock.ControlAsync(WiredClockControlType.Reset, Ct);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await RunClockAsync(2_200);
        await SaveClockBoxAsync<UpdateConditionMessage>(4, [1, 0, 0, 2]);
        matches.Evaluate(Context()).Should().BeTrue("started 2.2 s ago");
    }

    /// <summary>
    /// Measured (official client capture 2026-10-03): Clock Reaches Time at 3 s, the counter
    /// started and left running for 3.4 s, moved the target once (trigger.15).
    /// </summary>
    [Fact]
    public async Task Clock_reaches_time_fires_once_as_the_counter_passes_it_measured_clocks()
    {
        var clock = AddClock();
        var pad = _room.AddFloorItem(PAD, 3, 5);
        _room.AddBox<WiredTriggerCounterReachesTime>(TRIGGER, 0, 0, "wf_trg_clock_counter");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        await SaveClockBoxAsync<UpdateTriggerMessage>(TRIGGER, [3, 0, 0]);
        await SaveToggleAsync();
        await StackChangedAsync();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        var fired = 0;

        for (var at = 0; at < 6_000; at += 50)
        {
            var before = pad.Logic.GetState();
            _room.Clock.AdvanceMs(50);
            await _room
                .Harness.Module<RoomTimerSystem>()
                .ProcessTimersAsync(_room.Harness.Room.NowMs(), Ct);
            await Wired.ProcessWiredAsync(_room.Harness.Room.NowMs(), dormant: false, Ct);

            if (pad.Logic.GetState() != before)
                fired++;
        }

        fired.Should().Be(1);
    }

    /// <summary>
    /// trigger.12.R5 / trigger.19.R5: the long and the short repeater fire again and again, and
    /// stop once the trigger is picked up out of the stack.
    /// </summary>
    [Theory]
    [InlineData("trigger.12.R5", "wf_trg_period_long", 1, 16_000)]
    [InlineData("trigger.19.R5", "wf_trg_period_short", 2, 1_000)]
    public async Task A_repeater_repeats_and_stops_once_picked_up(
        string caseId,
        string name,
        int units,
        long runMs
    )
    {
        var pad = _room.AddFloorItem(PAD, 3, 3);

        if (name == "wf_trg_period_long")
            _room.AddBox<WiredTriggerLongPeriodically>(TRIGGER, 0, 0, name);
        else
            _room.AddBox<WiredTriggerShortPeriodically>(TRIGGER, 0, 0, name);

        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [units]))
            .Should()
            .BeTrue();
        await SaveToggleAsync();
        await StackChangedAsync();

        var fired = await RunAsync(runMs, pad);
        fired.Should().BeGreaterThan(1, $"{caseId}: it repeats");

        await _room
            .Harness.Module<RoomObjectModule>()
            .RemoveObjectAsync(Owner, _room.FloorItem(TRIGGER), Ct, 1);

        (await RunAsync(runMs, pad)).Should().Be(0, $"{caseId}: picked up, it stops");
    }

    private FurnitureCounterClockLogic AddClock() =>
        (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;

    // A clock box picking the counter.
    private async Task SaveClockBoxAsync<TMessage>(int id, int[] ints)
        where TMessage : UpdateWiredMessage => (
            await _room.SaveAsync<TMessage>(
                id,
                intParams: ints,
                stuffIds: [CLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: typeof(TMessage) == typeof(UpdateTriggerMessage) ? null : [0]
            )
        ).Should().BeTrue();

    // EFFECT toggles PAD.
    private async Task SaveToggleAsync() => (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [PAD],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private Task StackChangedAsync() =>
        Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Owner,
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Owner,
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

    // Wired ticks every 50 ms for `forMs`; each PAD toggle is a firing.
    private async Task<int> RunAsync(long forMs, IRoomFloorItem pad)
    {
        var fired = 0;
        var until = _now + forMs;

        while (_now < until)
        {
            var before = pad.Logic.GetState();
            _now += 50;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

            if (pad.Logic.GetState() != before)
                fired++;
        }

        return fired;
    }

    private async Task RunClockAsync(long forMs)
    {
        var timers = _room.Harness.Module<RoomTimerSystem>();

        for (var at = 0; at < forMs; at += 50)
        {
            _room.Clock.AdvanceMs(50);
            await timers.ProcessTimersAsync(_room.Harness.Room.NowMs(), Ct);
        }
    }

    private async Task RunClockUntilAsync(FurnitureCounterClockLogic clock, int seconds)
    {
        var timers = _room.Harness.Module<RoomTimerSystem>();

        for (var i = 0; i < 400 && clock.GetState() < seconds; i++)
        {
            _room.Clock.AdvanceMs(50);
            await timers.ProcessTimersAsync(_room.Harness.Room.NowMs(), Ct);
        }

        clock.GetState().Should().Be(seconds);
    }
}
