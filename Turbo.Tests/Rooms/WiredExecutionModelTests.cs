using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
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
        _room.AddFloorItem(ANTENNA_A, 8, 8, "wf_antenna1");
        _room.AddFloorItem(ANTENNA_B, 8, 9, "wf_antenna1");
    }

    [Fact]
    public async Task A_signal_ping_pong_runs_100_visits_per_stack_inside_the_one_click()
    {
        // Wall-clock limits off: a slow test machine must not cut or flag the chain.
        Configure(
            new WiredConfig
            {
                MaxRoomLogEntries = 10_000,
                RecursionTimeoutMs = 0,
                HeavyBusyMsPerWindow = 0,
            }
        );
        await BuildPingPongAsync();
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        // Habbo: rings of 2, 12 and 24 stacks stopped at 200, 1200 and 2400 visits.
        Logged("ping").Should().Be(100);
        Logged("pong").Should().Be(100);

        // Habbo read 170.9 after one click on the same build (WIN63-202610131318), then x0.6 a
        // half second: 201 stack runs and 401 actions at the default costs.
        Wired.GetExecutionCost(_now).Should().BeApproximately(201 * 0.05 + 401 * 0.4, 1e-6);
        Wired
            .GetExecutionCost(_now + 500)
            .Should()
            .BeApproximately((201 * 0.05 + 401 * 0.4) * 0.6, 1e-6);

        await TickAsync(5);

        Logged("ping").Should().Be(100, "the visit past the cap is dropped, not carried on");
        Logged("pong").Should().Be(100);
        Wired.GetErrorLogs(_now).Should().OnlyContain(x => x.ThrowCount == 0);
    }

    [Fact]
    public async Task A_timer_feeding_the_ping_pong_runs_the_chain_once_per_half_second_window()
    {
        Configure(
            new WiredConfig
            {
                MaxRoomLogEntries = 100_000,
                RecursionTimeoutMs = 0,
                HeavyBusyMsPerWindow = 0,
            }
        );
        await BuildStackAsync(0, 0, shortPeriodUnits: 1, actions: [Signal(ANTENNA_A)]);
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
        await StartAsync();

        // 2 s of 50 ms ticks: the timer sends every tick, but the receivers used their 100 visits
        // for the window, so the chain runs once in each of the five windows the ticks touch
        // (Habbo, 20 Hz timer into the ping-pong: one 200-step chain per ~500 ms, the antenna
        // alone flashing in between).
        for (var i = 0; i < 40; i++)
        {
            _now += 50;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        Logged("ping").Should().Be(500);
        Logged("pong").Should().Be(500);

        // Habbo's usage settled at 432-449 under the same load.
        Wired.GetExecutionCost(_now).Should().BeInRange(380, 460);
    }

    [Fact]
    public async Task A_delayed_signal_loop_is_a_new_execution_each_time_and_keeps_going()
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000, RecursionTimeoutMs = 0 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Signal(ANTENNA_A)]);
        await BuildStackAsync(
            0,
            2,
            receiveAntenna: ANTENNA_A,
            actions: [Log("loop"), Signal(ANTENNA_A, delayPulses: 1)],
            inOrder: true
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

    [Theory]
    [InlineData(10, 2)]
    [InlineData(2, 10)]
    [InlineData(1, 20)]
    public async Task A_short_timer_keeps_its_rate_with_its_actions_run_in_the_firing_tick(
        int units,
        int perSecond
    )
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000 });
        await BuildStackAsync(0, 0, shortPeriodUnits: units, actions: [Log("tick")]);
        await StartAsync();

        // Habbo: short-period units 10, 2 and 1 gave about 2, 10 and 20 updates a second.
        for (var i = 0; i < 40; i++)
        {
            _now += 50;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        Logged("tick").Should().Be(2 * perSecond);
    }

    [Fact]
    public async Task A_20_Hz_timer_keeps_its_rate_when_ticks_come_late()
    {
        Configure(new WiredConfig { MaxRoomLogEntries = 10_000 });
        await BuildStackAsync(0, 0, shortPeriodUnits: 1, actions: [Log("tick")]);
        await StartAsync();

        // The room's loop calls in every 40 ms, so a 50 ms tick often runs up to 30 ms late.
        for (var i = 0; i < 100; i++)
        {
            _now += 40;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        // 4 s at 20 a second; counting each period from the late tick gave 50.
        Logged("tick").Should().BeInRange(78, 80);
    }

    [Fact]
    public async Task A_half_second_repeater_fires_on_the_avatar_tick()
    {
        // Wired starts a third of the way between two avatar ticks (they fall every 500 ms).
        _now = 10_130;
        await BuildStackAsync(0, 0, periodic: true, actions: [Log("repeat")]);
        await StartAsync();

        // 11 130: the repeater is first seen. A period from then is 11 630, between ticks; it waits
        // for the avatar tick at 12 000 and then keeps every 500 ms on the grid.
        for (var at = 11_150L; at <= 13_000; at += 50)
        {
            _now = at;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

            if (at == 11_950)
                Logged("repeat").Should().Be(0, "11 630 is not an avatar tick");
            if (at == 12_000)
                Logged("repeat").Should().Be(1);
        }

        Logged("repeat").Should().Be(3, "12 000, 12 500 and 13 000");
    }

    [Fact]
    public async Task An_execution_past_the_usage_cap_is_cancelled_and_counted_and_usage_decays()
    {
        Configure(
            new WiredConfig
            {
                ExecutionCostCap = 5,
                ExecutionCostCapPerOwnerLevel = 0,
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
        Wired.GetExecutionCost(_now + 1_000).Should().BeApproximately(6 * 0.6 * 0.6, 1e-9);

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
        Configure(new WiredConfig { HeavyBusyMsPerWindow = 1e-9, HeavyWindowMs = 3_000 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("busy")]);
        await StartAsync();

        // Two executions in one three-second window, which is judged when it ends.
        await ClickAsync();
        await TickAsync(1);
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
    public async Task Loading_the_rooms_boxes_is_not_wired_time_and_does_not_mark_it_heavy()
    {
        Configure(new WiredConfig { HeavyBusyMsPerWindow = 1e-9, HeavyWindowMs = 1_000 });

        await BuildPingPongAsync();
        await StartAsync();
        await TickAsync(3);

        Wired.IsHeavy.Should().BeFalse();
        Wired.GetErrorLogs(_now)[3].ThrowCount.Should().Be(0);
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

    /// <summary>
    /// A limit Habbo has a bubble text for (room.error.wired.delayed_events_cap) is told as a
    /// wired.error bubble, once per cooldown however often it is hit, to the room's owner.
    /// </summary>
    [Fact]
    public async Task Hitting_a_limit_tells_the_room_owner_once_per_cooldown()
    {
        Configure(new WiredConfig { MaxDelayedEvents = 3 });

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("later", delayPulses: 2)]);
        await StartAsync();

        for (var i = 0; i < 5; i++)
            await ClickAsync();

        await TickAsync(4);

        Wired.GetErrorLogs(_now)[1].ThrowCount.Should().Be(2);
        // Filtered by its text: on a loaded machine the room may also be marked heavy.
        var notice = ErrorNotices()
            .Where(x => x.Notice.Parameters["message"] == "${room.error.wired.delayed_events_cap}")
            .Should()
            .ContainSingle()
            .Subject;
        notice.Owner.Should().Be(1);
        notice.Notice.NotificationType.Should().Be("wired.error");
    }

    /// <summary>
    /// Over the usage cap Habbo told the owner once, then every 20 s while the room stayed over
    /// it, and counted EXECUTION_CAP once per bubble (official client capture 2026-10-20).
    /// </summary>
    [Fact]
    public async Task The_usage_cap_is_told_and_counted_once_per_20_seconds()
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

        for (var round = 0; round < 3; round++)
        {
            await ClickAsync();
            await TickAsync(10);
        }

        Wired.GetErrorLogs(_now)[0].ThrowCount.Should().Be(2);
        ErrorNotices()
            .Select(x => x.Notice.Parameters["message"])
            .Should()
            .Equal("${room.error.wired.execution_cap}", "${room.error.wired.execution_cap}");
        ErrorNotices()
            .Should()
            .OnlyContain(x =>
                x.Notice.Parameters["image"] == "${image.library.url}album1584/WIRD2.png"
            );
    }

    [Fact]
    public async Task No_one_is_told_when_the_hotel_turns_the_bubbles_off()
    {
        Configure(
            new WiredConfig
            {
                MaxDelayedEvents = 3,
                ErrorNoticeRecipients = WiredErrorNoticeRecipients.None,
            }
        );

        await BuildStackAsync(0, 0, clickTrigger: true, actions: [Log("later", delayPulses: 2)]);
        await StartAsync();

        for (var i = 0; i < 5; i++)
            await ClickAsync();

        await TickAsync(4);

        Wired.GetErrorLogs(_now)[1].ThrowCount.Should().Be(2);
        ErrorNotices().Should().BeEmpty();
    }

    private List<(object? Owner, NotificationDialogMessageComposer Notice)> ErrorNotices() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("SendComposerAsync")
                .SelectMany(c =>
                    c.Args.OfType<NotificationDialogMessageComposer>()
                        .Select(n => (Owner: Key(c.Key), Notice: n))
                ),
        ];

    private static object? Key(object? key) =>
        key switch
        {
            PlayerId p => p.Value,
            long n => (int)n,
            _ => key,
        };

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
        int shortPeriodUnits = 0,
        int receiveAntenna = 0,
        ActionSpec[]? actions = null,
        bool inOrder = false
    )
    {
        var triggerId = _nextBoxId++;

        // Effects run in a random order unless the stack says otherwise.
        if (inOrder)
            _room.AddBox<WiredAddonExecuteInOrder>(_nextBoxId++, x, y, "wf_xtra_exec_in_order");

        if (shortPeriodUnits > 0)
        {
            _room.AddBox<WiredTriggerShortPeriodically>(triggerId, x, y, "wf_trg_period_short");
            (await _room.SaveAsync<UpdateTriggerMessage>(triggerId, intParams: [shortPeriodUnits]))
                .Should()
                .BeTrue();
        }
        else if (periodic)
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

    private void Configure(WiredConfig config) => _room.Harness.Room.UseWiredConfig(config);

    private void UseWallClockAdvancing(long stepMs) =>
        Wired.UseWallClock(
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
