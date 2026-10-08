using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Rooms.Wired.Variables.Furniture.Smart;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The counters as the official client's Creator Tools show them (captured 2026-10-08, a Small
/// Wired Counter, a Wired Game Counter and a Banzai counter in one room):
/// <c>~clock.state</c> is 0 Initial, 1 Running, 2 Paused, and <c>@state</c> follows it; the Wired
/// Game Counter counts up like the Small Wired Counter; <c>~clock.is_game_aware</c> is held by
/// the Wired Game Counter and the Banzai counter and not by the Small Wired Counter; the Banzai
/// counter counts the game down, with <c>~clock.pulse_count</c> the time left, and is Paused once
/// the game is over. The Wired Game Counter was a countdown that ran the game, the game timers
/// had no logic, and neither variable existed.
/// </summary>
public sealed class WiredClockStateTests
{
    private const int SMALL = 30;
    private const int GAME = 31;
    private const int BANZAI = 32;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    [Fact]
    public async Task A_counter_goes_initial_running_paused_and_back_on_reset()
    {
        var clock = Add<FurnitureCounterClockLogic>(SMALL, "wf_upcounter1");
        var state = new FurnitureClockStateVariable(_room.Harness.Room);
        var furniState = new FurnitureStateVariable(_room.Harness.Room);

        Read(state, SMALL).Should().Be(0);

        await clock.OnUseAsync(Owner, 0, Ct);
        Read(state, SMALL).Should().Be(1);
        Read(furniState, SMALL).Should().Be(1);

        await clock.OnUseAsync(Owner, 0, Ct);
        Read(state, SMALL).Should().Be(2);
        Read(furniState, SMALL).Should().Be(2);

        await clock.OnUseAsync(Owner, 2, Ct);
        Read(state, SMALL).Should().Be(0);

        state
            .GetVarSnapshot()
            .TextConnectors.Should()
            .BeEquivalentTo(
                new Dictionary<WiredVariableValue, string>
                {
                    [0] = "Initial",
                    [1] = "Running",
                    [2] = "Paused",
                }
            );
    }

    [Fact]
    public async Task The_game_counter_counts_up_and_only_game_counters_are_game_aware()
    {
        Add<FurnitureCounterClockLogic>(SMALL, "wf_upcounter1");
        var game = Add<FurnitureGameCounterLogic>(GAME, "wf_game_upcounter1");
        var aware = new FurnitureClockIsGameAwareVariable(_room.Harness.Room);

        await game.OnUseAsync(Owner, 0, Ct);
        await TickAsync(3, intervalMs: 500);

        game.HalfSeconds.Should().BeGreaterThan(0);
        game.ClockState.Should().Be(WiredClockState.Running);
        Holds(aware, GAME).Should().BeTrue();
        Holds(aware, SMALL).Should().BeFalse();
    }

    [Fact]
    public async Task A_game_timer_counts_the_game_down_and_game_aware_counters_hold_when_it_ends()
    {
        var game = Add<FurnitureGameCounterLogic>(GAME, "wf_game_upcounter1");
        var item = _room.AddFloorItem(BANZAI, 5, 5, "bb_counter");
        var timer = _room.Harness.LogicProvider.CreateLogicInstance(
            "default_floor",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        timer.Should().BeOfType<FurnitureGameTimerLogic>();
        item.GetType().GetMethod("SetLogic")!.Invoke(item, [timer]);

        var banzai = (FurnitureGameTimerLogic)timer;
        var pulses = new FurnitureClockPulseCountVariable(_room.Harness.Room);
        var aware = new FurnitureClockIsGameAwareVariable(_room.Harness.Room);

        await banzai.OnUseAsync(Owner, 0, Ct);
        await TickAsync(1);
        await game.OnUseAsync(Owner, 0, Ct);
        game.ClockState.Should().Be(WiredClockState.Running);

        banzai.ClockState.Should().Be(WiredClockState.Running);
        Read(pulses, BANZAI).Should().Be(58, "30 s, one second gone, in half seconds");
        Holds(aware, BANZAI).Should().BeTrue();

        // Ending the game early: the timer and the running game counter both hold.
        await banzai.OnUseAsync(Owner, 0, Ct);

        banzai.ClockState.Should().Be(WiredClockState.Paused);
        game.ClockState.Should().Be(WiredClockState.Paused);
    }

    private T Add<T>(int id, string classname)
        where T : class =>
        (T)
            (object)
                _room
                    .AddFloorItem(
                        id,
                        id - 28,
                        2,
                        classname,
                        createLogic: (factory, ctx) =>
                            (Turbo.Primitives.Rooms.Object.Logic.IRoomObjectLogic)
                                Activator.CreateInstance(typeof(T), factory, ctx)!
                    )
                    .Logic;

    /// <summary>
    /// Runs each clock's next tick once: the room's time is its stopwatch, and a tick schedules the
    /// next from then, so stepping exactly one interval ahead runs only the one that is due.
    /// </summary>
    private async Task TickAsync(int ticks, int intervalMs = 1_000)
    {
        var timers = _room.Harness.Module<RoomTimerSystem>();
        var nowMs = typeof(Turbo.Rooms.Grains.RoomGrain).GetMethod(
            "NowMs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;

        for (var i = 0; i < ticks; i++)
            await timers.ProcessTimersAsync(
                (long)nowMs.Invoke(_room.Harness.Room, null)! + intervalMs,
                Ct
            );
    }

    private static long Read(WiredInternalVariable variable, int id)
    {
        variable.TryGetValue(Key(variable, id), out var value).Should().BeTrue();

        return value.Value;
    }

    private static bool Holds(WiredInternalVariable variable, int id) =>
        variable.TryGetValue(Key(variable, id), out _);

    private static WiredVariableKey Key(WiredInternalVariable variable, int id) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.Furni, id);
}
