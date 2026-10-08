using System;
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

    private int _remainingSeconds;
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

        await SetStateAsync(_roomGrain._roomConfig.GameDefaultDurationSeconds);
    }

    private async Task StartAsync(CancellationToken ct)
    {
        var duration =
            GetState() > 0 ? GetState() : _roomGrain._roomConfig.GameDefaultDurationSeconds;

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
