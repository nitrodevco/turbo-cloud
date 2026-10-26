using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

/// <summary>
/// The game timers (the Battle Banzai, Football and Freeze counters: bb_counter, fball_counter,
/// es_counter). Its state is the remaining seconds. A use by someone with rights starts a game
/// with the room wired: scores reset, the "game starts" trigger fires, the timer counts down and
/// "game ends" fires at zero. Using it again while it runs ends the game early. The official
/// client shows a Banzai counter with <c>~clock.is_game_aware</c>, <c>~clock.state</c> Running
/// while it counts and Paused once the game is over, and <c>~clock.pulse_count</c> as the time
/// left in pulses.
/// Its reset ("2", the counter's reset sprite) picks the game's time: each one the next of
/// 30, 60, 120, 180, 300 and 600 seconds and round again (Sulake's furni data
/// <c>CountDownClockFurniture</c> "30,60,120,180,300,600"); once a game ran down, the first reset
/// shows the chosen time again without moving on, and a start from 0 starts it (official client
/// capture 2026-10-26, session 012541 t 250.3-263.1, 315-318, 1806.3-1821.0: 13/13 moves on,
/// 1/1 back to the chosen time; start from 0, t 1444.7).
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurnitureGameTimerLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx),
        IWiredClock
{
    public const string LOGIC_NAME = "game_timer";

    /// <summary>The classnames of Sulake's game timers.</summary>
    public static readonly string[] CLASSNAMES = ["bb_counter", "fball_counter", "es_counter"];

    private const int TICK_MS = 1000;
    private const int USE_RESET = 2;

    /// <summary>The times a reset goes through, in seconds.</summary>
    public static readonly int[] DURATIONS = [30, 60, 120, 180, 300, 600];

    private int _remainingSeconds;

    /// <summary>The time a reset last chose; none (0) until one did or a game started.</summary>
    private int _chosenSeconds;
    private bool _isRunning;
    private bool _started;

    public int HalfSeconds => (_isRunning || _started ? _remainingSeconds : GetState()) * 2;

    public WiredClockState ClockState =>
        _isRunning ? WiredClockState.Running
        : _started ? WiredClockState.Paused
        : WiredClockState.Initial;

    public bool IsGameAware => true;

    public async Task ControlAsync(WiredClockControlType control, CancellationToken ct)
    {
        switch (control)
        {
            case WiredClockControlType.Start:
            case WiredClockControlType.Resume:
                if (!_isRunning)
                    await StartAsync(ct);
                break;
            case WiredClockControlType.Stop:
            case WiredClockControlType.Pause:
                await StopAsync(ct);
                break;
            case WiredClockControlType.Reset:
                await ResetAsync(ct);
                break;
        }
    }

    public async Task AdjustAsync(WiredOperatorType op, int halfSeconds, CancellationToken ct)
    {
        var current = _isRunning || _started ? _remainingSeconds : GetState();
        var seconds = halfSeconds / 2;
        var next = op switch
        {
            WiredOperatorType.Add => current + seconds,
            WiredOperatorType.Subtract => current - seconds,
            _ => seconds,
        };

        _remainingSeconds = Math.Max(0, next);
        await SetStateAsync(_remainingSeconds);
    }

    /// <summary>The game ended some other way: the timer holds where it is.</summary>
    public Task OnGameEndedAsync(CancellationToken ct)
    {
        if (_isRunning)
        {
            _isRunning = false;
            TimerSystem.Cancel(_ctx.ObjectId);
        }

        return Task.CompletedTask;
    }

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanToggle() => false;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        if (param == USE_RESET)
        {
            await ResetAsync(ct);

            return;
        }

        if (_isRunning)
        {
            await StopAsync(ct);

            return;
        }

        await StartAsync(ct);
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        await StopAsync(ct);

        _started = false;

        var chosen = ChosenSeconds();

        // Inference: a time other than the chosen one (a game stopped early, wired) goes back to
        // it as the 0 a game ran down to did.
        _chosenSeconds = GetState() == chosen ? NextDuration(chosen) : chosen;

        await SetStateAsync(_chosenSeconds);
    }

    /// <summary>The time a reset chose; before any, what the counter shows, or the default.</summary>
    private int ChosenSeconds() =>
        _chosenSeconds > 0 ? _chosenSeconds
        : GetState() > 0 ? GetState()
        : RoomConfig.GameDefaultDurationSeconds;

    private static int NextDuration(int seconds) =>
        DURATIONS.FirstOrDefault(x => x > seconds) is var next && next > 0 ? next : DURATIONS[0];

    private async Task StartAsync(CancellationToken ct)
    {
        var duration = GetState() > 0 ? GetState() : ChosenSeconds();

        if (_chosenSeconds == 0)
            _chosenSeconds = duration;

        // From 0 the counter shows the time first (t 1444.7: "300", then the start).
        if (GetState() == 0)
            await SetStateAsync(duration);

        _remainingSeconds = duration;
        _isRunning = true;
        _started = true;

        await GameSystem.StartGameAsync(ct);

        Schedule();
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _isRunning = false;

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task StopAsync(CancellationToken ct)
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        TimerSystem.Cancel(_ctx.ObjectId);

        await GameSystem.EndGameAsync(ct);
    }

    private void Schedule() => TimerSystem.Schedule(_ctx.ObjectId, TICK_MS, TickAsync);

    private async Task TickAsync(CancellationToken ct)
    {
        if (!_isRunning)
            return;

        _remainingSeconds = Math.Max(0, _remainingSeconds - 1);

        await SetStateAsync(_remainingSeconds);

        if (_remainingSeconds == 0)
        {
            await StopAsync(ct);

            return;
        }

        Schedule();
    }
}
