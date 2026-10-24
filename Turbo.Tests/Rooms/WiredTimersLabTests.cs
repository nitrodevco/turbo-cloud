using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Timers and delays as Habbo runs them (official client captures 2026-10-24,
/// WIN63-202610201032, wired matrix W6): Timer Reset in a repeater's stack fires that stack on
/// every 500 ms tick and holds "at given time" off until the resets stop (action.1.R2); a
/// repeater keeps firing while its delayed effects wait, each firing running once, exactly the
/// delay after it (trigger.6.R3, trigger.19.R3, trigger.12.R3); a delayed effect still runs
/// after its box was moved off the stack, and flashes when the stack fires, not when it runs
/// (action.38.R7).
/// </summary>
public sealed class WiredTimersLabTests
{
    private const int OWNER = 1;
    private const int COUNTER = 40;

    private readonly WiredRoom _room = new(10, 10);
    private int _nextBoxId = 1;
    private readonly List<int> _stackIds = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredTimersLabTests()
    {
        _room.Enter(5, 9, 9);
    }

    /// <summary>The room's time: its clock, which only <see cref="StepAsync"/> moves.</summary>
    private long Now => _room.Harness.NowMs();

    [Fact]
    public async Task Action1R2_ATimerResetInARepeatersStackFiresItOnEveryAvatarTick()
    {
        // Periodically [6] = 3 s, as in the lab.
        await PeriodicStackAsync(0, 0, units: 6, Log("fired"));
        var reset = await ResetTimersAsync(0, 0);
        await StartAsync();

        var firings = await RunAsync(12_000, "fired");

        // Habbo: 3 s to the first firing, then one on every 500 ms tick (19 in 9.5 s).
        firings.Count.Should().BeGreaterThanOrEqualTo(15);
        Gaps(firings).Should().AllBeEquivalentTo(500L);

        // Moved off: the reset of the last firing still brings the next tick, then 3 s again
        // (official: 22 411 reset, 22 649 moved off, fired 22 904 and 25 904).
        await MoveAsync(reset, 4, 4);
        var after = await RunAsync(7_000, "fired");

        after.Should().HaveCount(3);
        after[0].Should().Be(firings[^1] + 500);
        Gaps(after).Should().AllBeEquivalentTo(3_000L);
    }

    [Fact]
    public async Task Action1R2_AtGivenTimeWaitsUntilTheResetsStop()
    {
        await PeriodicStackAsync(0, 0, units: 6, Log("fired"));
        var reset = await ResetTimersAsync(0, 0);
        await AtGivenTimeStackAsync(2, 2, units: 2, Log("at time"));
        await StartAsync();

        var fired = new List<long>();
        var atTime = new List<long>();
        await RunAsync(8_000, ("fired", fired), ("at time", atTime));

        atTime.Should().NotContain(at => at > fired[0], "a reset every 500 ms never lets 1 s pass");

        await MoveAsync(reset, 4, 4);
        var movedAt = Now;
        var after = await RunAsync(3_000, "at time");

        // Habbo: 1.25 s after the box was moved off, 1.5 s after the last reset (1 sample); ours
        // fires its 1 s after the last reset, on the next tick.
        after.Should().ContainSingle().Which.Should().BeInRange(movedAt + 500, movedAt + 1_500);
    }

    [Fact]
    public async Task Action1R2_AHotelMayKeepRepeatersOutOfTimerReset()
    {
        _room.Harness.Room.UseWiredConfig(
            new WiredConfig { ResetTimersRestartsPeriodicTriggers = false }
        );
        await PeriodicStackAsync(0, 0, units: 6, Log("fired"));
        await ResetTimersAsync(0, 0);
        await StartAsync();

        Gaps(await RunAsync(12_000, "fired")).Should().AllBeEquivalentTo(3_000L);
    }

    [Theory]
    // Periodically [2] (1 s), period short [10] (500 ms): an effect delayed 6 pulses (3 s).
    [InlineData("wf_trg_periodically", 2, 6, 1_000)]
    [InlineData("wf_trg_period_short", 10, 6, 500)]
    // Period long [1] (5 s): an effect delayed 14 pulses (7 s).
    [InlineData("wf_trg_period_long", 1, 14, 5_000)]
    public async Task Trigger6R3_ARepeaterKeepsFiringWhileItsDelayedEffectsWait(
        string trigger,
        int units,
        int delayPulses,
        long periodMs
    )
    {
        await PeriodicStackAsync(0, 0, units, Log("fired"), Log("ran", delayPulses), trigger);
        await StartAsync();

        var fired = new List<long>();
        var ran = new List<long>();
        await RunAsync(20_000, ("fired", fired), ("ran", ran));

        Gaps(fired).Should().AllBeEquivalentTo(periodMs, "pending runs do not hold the repeater");
        ran.Should().NotBeEmpty();
        ran.Should().Equal(fired.Take(ran.Count).Select(at => at + delayPulses * 500L));
        ran.Count.Should().Be(fired.Count(at => at + delayPulses * 500L <= Now));
    }

    [Fact]
    public async Task Action38R7_ADelayedEffectMovedOffItsStackStillRunsAndFlashedWhenItFired()
    {
        // Periodically [2] (1 s) with an effect delayed 6 pulses (3 s).
        await PeriodicStackAsync(0, 0, units: 2, Log("ran", delayPulses: 6));
        var delayed = _nextBoxId - 1;
        await StartAsync();

        (await RunAsync(5_500, "ran")).Should().NotBeEmpty();
        var flashesOnTheStack = Flashes(delayed);

        // Official: the 3 runs still pending when it was moved off changed the counter, unflashed.
        await MoveAsync(delayed, 4, 4);
        var movedAt = Now;
        var after = await RunAsync(5_000, "ran");

        after.Should().HaveCount(3).And.OnlyContain(at => at <= movedAt + 3_000);
        flashesOnTheStack.Should().BePositive();
        Flashes(delayed).Should().Be(flashesOnTheStack, "a delayed effect flashed when it fired");
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 10)]
    public async Task Action28R6_ACounterRunningWhenItsRoomUnloadedComesBackStoppedAtZero(
        bool official,
        int seconds
    )
    {
        var counter = AddCounter(_room, COUNTER);
        await counter.ControlAsync(WiredClockControlType.Start, Ct);
        await CountAsync(_room, 21);
        counter.GetState().Should().Be(10);

        // What the room hands to persistence when it unloads, and the next load reads back.
        var stored = _room.FloorItem(COUNTER).ExtraData.GetJsonString();
        var reloaded = new WiredRoom(10, 10);
        reloaded.Harness.Room.UseWiredConfig(
            new WiredConfig { RunningClocksResetOnRoomLoad = official }
        );
        var again = AddCounter(reloaded, COUNTER, stored);
        await again.OnAttachAsync(Ct);
        await CountAsync(reloaded, 4);

        again.GetState().Should().Be(seconds);
        again.IsRunning.Should().BeFalse();

        // Started again it counts from where it came back.
        await again.ControlAsync(WiredClockControlType.Start, Ct);
        await CountAsync(reloaded, 3);
        again.GetState().Should().Be(seconds + 1);
    }

    [Fact]
    public async Task Action28R6_ACounterStoppedBeforeItsRoomUnloadedKeepsItsTime()
    {
        var counter = AddCounter(_room, COUNTER);
        await counter.ControlAsync(WiredClockControlType.Start, Ct);
        await CountAsync(_room, 21);
        await counter.ControlAsync(WiredClockControlType.Stop, Ct);

        var reloaded = new WiredRoom(10, 10);
        var again = AddCounter(
            reloaded,
            COUNTER,
            _room.FloorItem(COUNTER).ExtraData.GetJsonString()
        );
        await again.OnAttachAsync(Ct);

        again.GetState().Should().Be(10);
    }

    private static FurnitureGameCounter1Logic AddCounter(
        WiredRoom room,
        int id,
        string? extraData = null
    ) =>
        (FurnitureGameCounter1Logic)
            room.AddFloorItem(
                id,
                3,
                3,
                "wf_game_upcounter1",
                createLogic: (factory, ctx) =>
                {
                    ctx.RoomObject.SetExtraData(extraData);

                    return new FurnitureGameCounter1Logic(factory, ctx);
                }
            ).Logic;

    /// <summary>Runs the room's clocks for <paramref name="halfSeconds"/> half seconds.</summary>
    private static async Task CountAsync(WiredRoom room, int halfSeconds)
    {
        var timers = room.Harness.Module<RoomTimerSystem>();

        for (var i = 0; i < halfSeconds; i++)
        {
            room.Harness.Clock.AdvanceMs(500);
            await timers.ProcessTimersAsync(room.Harness.NowMs(), Ct);
        }
    }

    private async Task PeriodicStackAsync(
        int x,
        int y,
        int units,
        ActionSpec action,
        ActionSpec? second = null,
        string trigger = "wf_trg_periodically"
    )
    {
        var id = _nextBoxId++;

        switch (trigger)
        {
            case "wf_trg_period_short":
                _room.AddBox<WiredTriggerShortPeriodically>(id, x, y, trigger);
                break;
            case "wf_trg_period_long":
                _room.AddBox<WiredTriggerLongPeriodically>(id, x, y, trigger);
                break;
            default:
                _room.AddBox<WiredTriggerPeriodically>(id, x, y, trigger);
                break;
        }

        (await _room.SaveAsync<UpdateTriggerMessage>(id, intParams: [units])).Should().BeTrue();

        // In stacking order, so the effect without a delay runs at the firing.
        if (second is not null)
            _room.AddBox<WiredAddonExecuteInOrder>(_nextBoxId++, x, y, "wf_xtra_exec_in_order");

        await action.AddAndSave(_nextBoxId++, x, y);

        if (second is not null)
            await second.AddAndSave(_nextBoxId++, x, y);

        _stackIds.Add(_room.Map.ToIdx(x, y));
    }

    private async Task AtGivenTimeStackAsync(int x, int y, int units, ActionSpec action)
    {
        var id = _nextBoxId++;

        _room.AddBox<WiredTriggerAtTime>(id, x, y, "wf_trg_at_given_time");
        (await _room.SaveAsync<UpdateTriggerMessage>(id, intParams: [units])).Should().BeTrue();

        await action.AddAndSave(_nextBoxId++, x, y);

        _stackIds.Add(_room.Map.ToIdx(x, y));
    }

    private async Task<int> ResetTimersAsync(int x, int y)
    {
        var id = _nextBoxId++;

        _room.AddBox<WiredActionResetTimers>(id, x, y, "wf_act_reset_timers");
        (await _room.SaveAsync<UpdateActionMessage>(id, definitionSpecifics: [0]))
            .Should()
            .BeTrue();

        return id;
    }

    private sealed record ActionSpec(Func<int, int, int, Task> AddAndSave);

    private ActionSpec Log(string text, int delayPulses = 0) =>
        new(
            async (id, x, y) =>
            {
                _room.AddBox<WiredActionWriteToLogs>(id, x, y, "wf_act_log");
                (
                    await _room.SaveAsync<UpdateActionMessage>(
                        id,
                        intParams: [(int)WiredLogLevelType.Warning],
                        stringParam: text,
                        definitionSpecifics: [delayPulses]
                    )
                )
                    .Should()
                    .BeTrue();
            }
        );

    /// <summary>Moves a box the way a player does, then lets the room rebuild its stacks.</summary>
    private async Task MoveAsync(int boxId, int x, int y)
    {
        (
            await _room
                .Harness.Module<RoomFurniModule>()
                .MoveFloorItemAsync(
                    ActionContext.CreateForPlayer(OWNER, 1),
                    _room.FloorItem(boxId),
                    _room.Map.ToIdx(x, y),
                    null,
                    Rotation.North,
                    true,
                    Ct
                )
        )
            .Should()
            .BeTrue();
    }

    /// <summary>The room builds its stacks on the next tick after a box says its stack changed.</summary>
    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [.. _stackIds],
            },
            Ct
        );

        await StepAsync();
    }

    /// <summary>One 50 ms wired tick, the room's clock moved with it.</summary>
    private async Task StepAsync()
    {
        _room.Harness.Clock.AdvanceMs(50);
        await Wired.ProcessWiredAsync(Now, dormant: false, Ct);
    }

    /// <summary>Runs the room's 50 ms wired ticks for <paramref name="ms"/> and returns when <paramref name="text"/> was logged.</summary>
    private async Task<List<long>> RunAsync(long ms, string text)
    {
        var at = new List<long>();

        await RunAsync(ms, (text, at));

        return at;
    }

    private async Task RunAsync(long ms, params (string Text, List<long> At)[] watched)
    {
        var counts = watched.Select(w => Logged(w.Text)).ToArray();
        var end = Now + ms;

        while (Now < end)
        {
            await StepAsync();

            for (var i = 0; i < watched.Length; i++)
            {
                var count = Logged(watched[i].Text);

                for (; counts[i] < count; counts[i]++)
                    watched[i].At.Add(Now);
            }
        }
    }

    private static List<long> Gaps(List<long> at) => [.. at.Zip(at.Skip(1), (a, b) => b - a)];

    private int Logged(string text) => Wired.GetRoomLogPage(1, 10_000, -1, -1, text).TotalEntries;

    private int Flashes(int boxId) =>
        _room
            .Harness.Fakes.SentToRoom<Turbo.Primitives.Messages.Outgoing.Room.Engine.ObjectsDataUpdateMessageComposer>()
            .Count(update => update.StuffDatas.Any(entry => (int)entry.Item1 == boxId));
}
