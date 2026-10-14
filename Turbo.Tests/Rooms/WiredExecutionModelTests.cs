using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// How Habbo runs wired (official client captures, live room, 2026-10-10/11): a signal runs its
/// receivers inside the execution that sent it, each receiving stack at most 100 times per
/// execution, the visit past that dropped without an error; a delay starts a new execution with
/// fresh budgets. The room's usage is a decaying float; an execution that takes it past the cap is
/// cancelled and EXECUTION_CAP counted, while "heavy" is a separate warning. Pending delayed
/// executions are bounded (DELAYED_EVENTS_CAP), an execution running too long is stopped
/// (RECURSION_TIMEOUT), and a KILLED room runs no wired for 10 minutes (in-client error help).
/// Every build is written to the room log by "write to logs", which counts what ran.
/// </summary>
public sealed class WiredExecutionModelTests
{
    private const int CLICK_ME = 30;
    private const int ANTENNA_A = 31;
    private const int ANTENNA_B = 32;

    private readonly WiredRoom _room = new(10, 10);
    private long _now = 10_000;
    private long _wallMs;
    private int _nextBoxId = 1;
    private readonly List<int> _stackIds = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredExecutionModelTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 9, 9);
        _room.AddFloorItem(ANTENNA_A, 8, 8);
        _room.AddFloorItem(ANTENNA_B, 8, 9);
    }

    [Fact]
    public async Task A_signal_ping_pong_runs_100_visits_per_stack_inside_the_one_click()
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000 });
        await BuildPingPongAsync();
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        // Habbo: rings of 2, 12 and 24 stacks stopped at 200, 1200 and 2400 visits.
        Logged("ping").Should().Be(100);
        Logged("pong").Should().Be(100);

        await TickAsync(5);

        Logged("ping").Should().Be(100, "the visit past the cap is dropped, not carried on");
        Logged("pong").Should().Be(100);
        Wired.GetErrorLogs(_now).Should().OnlyContain(x => x.ThrowCount == 0);
    }

    [Fact]
    public async Task A_delayed_signal_loop_is_a_new_execution_each_time_and_keeps_going()
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Signal(ANTENNA_A)]);
        await BuildStackAsync(
            0,
            2,
            receiveAntenna: ANTENNA_A,
            actions: [Log("loop"), Signal(ANTENNA_A, delayPulses: 1)]
        );
        await StartAsync();

        await ClickAsync();
        await TickAsync(30);

        // One loop a tick, each a fresh execution: neither the visit cap nor a call depth ends it.
        Logged("loop").Should().Be(30);
    }

    [Fact]
    public async Task A_timer_runs_its_actions_and_their_signals_in_the_tick_it_fires()
    {
        await BuildStackAsync(0, 0, periodic: true, actions: [Log("timer"), Signal(ANTENNA_A)]);
        await BuildStackAsync(0, 2, receiveAntenna: ANTENNA_A, actions: [Log("received")]);
        await StartAsync();

        await TickAsync(1);

        Logged("timer").Should().Be(1);
        Logged("received").Should().Be(1);
    }

    [Fact]
    public async Task An_execution_past_the_usage_cap_is_cancelled_and_counted_and_usage_decays()
    {
        Configure(
            new WiredConfig
            {
                ExecutionCostCap = 5,
                ExecutionCostPerStack = 0,
                ExecutionCostPerAction = 1,
                ExecutionCostPerTarget = 0,
                HeavyBusyMsPerWindow = 1_000_000,
            }
        );

        await BuildStackAsync(
            0,
            0,
            clickTrigger: true,
            actions: [.. Enumerable.Range(1, 8).Select(i => Log($"step {i}"))]
        );
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        // Five actions fit the cap; the sixth takes the usage past it and the rest never run.
        Logged("step").Should().Be(5);
        Wired.GetErrorLogs(_now)[0].ErrorName.Should().Be("EXECUTION_CAP");
        Wired.GetErrorLogs(_now)[0].ThrowCount.Should().Be(1);

        var usage = Wired.GetExecutionCost(_now);

        usage.Should().Be(6);
        Wired.GetExecutionCost(_now + 1_000).Should().BeApproximately(6 * 0.6875 * 0.6875, 1e-9);

        // Over the cap, yet not heavy: Habbo logged EXECUTION_CAP with Heavy false.
        var stats = await _room.Harness.Room.GetWiredRoomStatsAsync(
            ActionContext.CreateForSystem(1),
            Ct
        );

        stats!.IsHeavy.Should().BeFalse();
        stats.ExecutionCostCap.Should().Be(5);
    }

    [Fact]
    public async Task A_room_whose_wired_takes_too_much_server_time_is_marked_heavy_and_keeps_running()
    {
        Configure(new WiredConfig { HeavyBusyMsPerWindow = 1e-9, HeavyWindowMs = 1_000 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("busy")]);
        await StartAsync();

        await ClickAsync();
        await TickAsync(2);
        await ClickAsync();
        await TickAsync(2);

        Wired.IsHeavy.Should().BeTrue();
        Logged("busy").Should().Be(2, "heavy is a warning, not a stop");

        var errors = Wired.GetErrorLogs(_now);

        errors[3].ErrorName.Should().Be("MARKED_AS_HEAVY");
        errors[3].ThrowCount.Should().Be(1, "it is counted when the room becomes heavy");
        errors[0].ThrowCount.Should().Be(0);
    }

    [Fact]
    public async Task Delayed_executions_past_the_cap_are_dropped_and_counted()
    {
        Configure(new WiredConfig { MaxDelayedEvents = 3 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("later", delayPulses: 2)]);
        await StartAsync();

        for (var i = 0; i < 5; i++)
            await ClickAsync();

        await TickAsync(4);

        Logged("later").Should().Be(3);
        Wired.GetErrorLogs(_now)[1].ErrorName.Should().Be("DELAYED_EVENTS_CAP");
        Wired.GetErrorLogs(_now)[1].ThrowCount.Should().Be(2);
    }

    [Fact]
    public async Task A_signal_chain_running_too_long_is_stopped_as_a_recursion_timeout()
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000, RecursionTimeoutMs = 100 });
        UseWallClockAdvancing(10);
        await BuildPingPongAsync();
        await StartAsync();

        await ClickAsync();
        await TickAsync(3);

        (Logged("ping") + Logged("pong")).Should().BeInRange(1, 20);
        Wired.GetErrorLogs(_now)[5].ErrorName.Should().Be("RECURSION_TIMEOUT");
        Wired.GetErrorLogs(_now)[5].ThrowCount.Should().Be(1);
    }

    [Fact]
    public async Task An_execution_past_the_hard_limit_kills_room_wired_for_ten_minutes()
    {
        Configure(new WiredConfig { KillExecutionMs = 50, RecursionTimeoutMs = 0 });
        UseWallClockAdvancing(100);

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("click")]);
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        Logged("click").Should().Be(1);
        Wired.GetErrorLogs(_now)[4].ErrorName.Should().Be("KILLED");
        Wired.GetErrorLogs(_now)[4].ThrowCount.Should().Be(1);
        Wired.IsKilled(_now).Should().BeTrue();

        await ClickAsync();
        await TickAsync(2);

        Logged("click").Should().Be(1, "a killed room runs no wired");

        _now += 600_000;
        UseWallClockAdvancing(0);
        await ClickAsync();
        await TickAsync(1);

        Logged("click").Should().Be(2, "the halt ends after ten minutes");
    }

    // --- builds ---

    private sealed record ActionSpec(Func<int, int, int, Task> AddAndSave);

    /// <summary>Starter (0,0) clicks into antenna A; (0,2) logs "ping" and signals B; (0,4) logs "pong" and signals A.</summary>
    private async Task BuildPingPongAsync()
    {
        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Signal(ANTENNA_A)]);
        await BuildStackAsync(
            0,
            2,
            receiveAntenna: ANTENNA_A,
            actions: [Log("ping"), Signal(ANTENNA_B)]
        );
        await BuildStackAsync(
            0,
            4,
            receiveAntenna: ANTENNA_B,
            actions: [Log("pong"), Signal(ANTENNA_A)]
        );
    }

    private async Task BuildStackAsync(
        int x,
        int y,
        bool clickTrigger = false,
        bool periodic = false,
        int receiveAntenna = 0,
        ActionSpec[]? actions = null
    )
    {
        var triggerId = _nextBoxId++;

        if (periodic)
        {
            // Every half second.
            _room.AddBox<WiredTriggerPeriodically>(triggerId, x, y, "wf_trg_periodically");
            (await _room.SaveAsync<UpdateTriggerMessage>(triggerId, intParams: [1]))
                .Should()
                .BeTrue();
        }
        else if (clickTrigger)
        {
            _room.AddBox<WiredTriggerClickFurni>(triggerId, x, y, "wf_trg_click_furni");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    triggerId,
                    stuffIds: [CLICK_ME],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();
        }
        else
        {
            _room.AddBox<WiredTriggerReceiveSignal>(triggerId, x, y, "wf_trg_recv_signal");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    triggerId,
                    stuffIds: [receiveAntenna],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();
        }

        foreach (var action in actions ?? [])
        {
            var id = _nextBoxId++;

            await action.AddAndSave(id, x, y);
        }

        _stackIds.Add(_room.Map.ToIdx(x, y));
    }

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

    private ActionSpec Signal(int antenna, int delayPulses = 0) =>
        new(
            async (id, x, y) =>
            {
                _room.AddBox<WiredActionSendSignal>(id, x, y, "wf_act_send_signal");
                (
                    await _room.SaveAsync<UpdateActionMessage>(
                        id,
                        intParams: [0, 0],
                        stuffIds: [antenna],
                        furniSources:
                        [
                            [WiredFurniSourceType.SelectedItems],
                            [WiredFurniSourceType.SelectedItems],
                        ],
                        playerSources:
                        [
                            [WiredPlayerSourceType.TriggeredUser],
                        ],
                        definitionSpecifics: [delayPulses]
                    )
                ).Should().BeTrue();
            }
        );

    private void Configure(WiredConfig config) =>
        RoomHarness.SetField(_room.Harness.Room, "_wiredConfig", config);

    private void UseWallClockAdvancing(long stepMs) =>
        RoomHarness.SetField(
            Wired,
            "_wallClockMs",
            (Func<long>)(
                () =>
                {
                    _wallMs += stepMs;

                    return _wallMs;
                }
            )
        );

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

        await TickAsync(1);
    }

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private int Logged(string text) => Wired.GetRoomLogPage(1, 10_000, -1, -1, text).TotalEntries;

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
