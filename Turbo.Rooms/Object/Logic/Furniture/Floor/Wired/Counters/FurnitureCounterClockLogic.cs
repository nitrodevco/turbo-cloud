using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

/// <summary>
/// The wired counters (wf_upcounter, and the game counters on top of it), which count up: the
/// official client shows a Wired Game Counter going 00:00, 00:01, 00:10 just as a Small Wired
/// Counter does. The item state is the elapsed time in seconds, which the client renders as
/// MM:SS. A use runs and holds it in turn (Initial, Running, Paused); the client "reset" tag
/// sends state 2. Wired controls it through the clock actions and reads it through the clock
/// trigger, condition and <c>~clock.*</c> variables, which receive a tick every half second.
/// </summary>
public class FurnitureCounterClockLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IWiredClock
{
    private const int USE_RESET = 2;
    private const string RUNNING_SECTION = "clockRunning";

    private int _halfSeconds;
    private bool _isRunning;
    private bool _started;

    // Stopped by Control Clock "stop" rather than held by "pause": the next start counts afresh.
    private bool _stopped;

    // When the next half second is due. Each tick is scheduled from this, not from when the room
    // tick got round to running the last one, so that lateness does not add up: rescheduling
    // 500 ms after each late run made a counter's second last ~1.1 s on a local server.
    private long _nextHalfSecondAtMs;

    public bool IsRunning => _isRunning;

    public WiredClockState ClockState =>
        _isRunning ? WiredClockState.Running
        : _started ? WiredClockState.Paused
        : WiredClockState.Initial;

    public virtual bool IsGameAware => false;

    public Task OnGameEndedAsync(CancellationToken ct) =>
        IsGameAware && _isRunning ? StopAsync() : Task.CompletedTask;

    /// <summary>Elapsed time in half seconds, the unit the wired clock boxes compare in.</summary>
    public int HalfSeconds => _halfSeconds;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanToggle() => false;

    public override Task OnAttachAsync(CancellationToken ct)
    {
        _halfSeconds = Math.Max(0, GetState()) * 2;

        // Habbo brings a counter that was running when its room unloaded back stopped at 0: one
        // running at 10 s was "0" in the reload's objects and did not tick, and Control Clock
        // started it from 0 (official client capture 2026-10-24, WIN63-202610201032, wired matrix
        // action.28.R6). One stopped before the unload is assumed to keep its time.
        if (WasRunning())
        {
            _ctx.RoomObject.ExtraData.DeleteSection(RUNNING_SECTION);

            if (WiredConfig.RunningClocksResetOnRoomLoad && _halfSeconds > 0)
            {
                _halfSeconds = 0;
                StuffData.SetState("0");
                PersistStuffData(false);
            }
        }

        _started = _halfSeconds > 0;

        return base.OnAttachAsync(ct);
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        switch (param)
        {
            case USE_RESET:
                await ControlAsync(WiredClockControlType.Reset, ct);
                break;
            default:
                await (_isRunning ? StopAsync() : StartAsync(ct));
                break;
        }
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _isRunning = false;
        MarkRunning(false);

        return base.OnPickupAsync(ctx, ct);
    }

    public Task ControlAsync(WiredClockControlType control, CancellationToken ct) =>
        control switch
        {
            WiredClockControlType.Start => StartAfreshAsync(ct),
            WiredClockControlType.Stop => StopForGoodAsync(),
            WiredClockControlType.Reset => ResetAsync(ct),
            // Pause holds the time and Resume runs on from it.
            WiredClockControlType.Pause => StopAsync(),
            WiredClockControlType.Resume => StartAsync(ct),
            _ => Task.CompletedTask,
        };

    /// <summary>
    /// Sets, adds or subtracts time; the display follows on the next second. The time jumps
    /// there: Clock Reaches Time does not fire for it (see <see cref="PublishAsync"/>).
    /// </summary>
    public Task AdjustAsync(WiredOperatorType op, int halfSeconds, CancellationToken ct)
    {
        var next = op switch
        {
            WiredOperatorType.Add => _halfSeconds + halfSeconds,
            WiredOperatorType.Subtract => _halfSeconds - halfSeconds,
            _ => halfSeconds,
        };

        _halfSeconds = Math.Clamp(next, 0, WiredConfig.ClockMaxHalfSeconds);

        return PublishAsync(ct, reached: false);
    }

    /// <summary>
    /// Control Clock "start". A counter stopped before counts afresh from 0 (13, 26 and 33 went
    /// to 0, 3/3); one never started counts on from the time Adjust Clock gave it (30 went 31,
    /// 32, 1/1) (official client capture 2026-10-24, action.28.R8).
    /// </summary>
    private async Task StartAfreshAsync(CancellationToken ct)
    {
        if (_isRunning)
            return;

        if (_stopped)
        {
            _halfSeconds = 0;

            await PublishAsync(ct, reached: false);
        }

        await StartAsync(ct);
    }

    private Task StopForGoodAsync()
    {
        _stopped = true;

        return StopAsync();
    }

    private async Task StartAsync(CancellationToken ct)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _started = true;
        _stopped = false;
        MarkRunning(true);

        // The count runs on the room's 500 ms cycle: the first cycle after the start is half a
        // second, the second shows "1", 500..1000 ms after the start (official client capture
        // 2026-10-16, five Control Clock starts: 597, 600, 849, 864 and 876 ms, each on the cycle
        // the walking avatar's status updates kept).
        _nextHalfSecondAtMs = _roomGrain.AlignToNextBoundary(NowMs() + 1, RoomConfig.AvatarTickMs);

        Schedule();

        // Starting the Wired Game Counter fired Game Starts (official client capture 2026-10-24,
        // action.6.R1). Inference: a game already running goes on rather than restarting.
        if (IsGameAware && !GameSystem.IsGameRunning)
            await GameSystem.StartGameAsync(ct);
    }

    private Task StopAsync()
    {
        _isRunning = false;
        MarkRunning(false);

        TimerSystem.Cancel(_ctx.ObjectId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Control Clock "reset". On a counter never started it does nothing, even one Adjust Clock
    /// set to 30 (4/4; started and stopped first, the same 30 reset to 0, 1/1; official client
    /// capture 2026-10-24, action.28.R7b).
    /// </summary>
    private async Task ResetAsync(CancellationToken ct)
    {
        if (!_started)
            return;

        await StopAsync();

        _halfSeconds = 0;
        _started = false;
        _stopped = false;

        await PublishAsync(ct, reached: false);
    }

    /// <summary>
    /// Keeps in the furni's extra data whether the counter runs, so the room's next load can tell
    /// a counter left running from one stopped at the same time.
    /// </summary>
    private void MarkRunning(bool running)
    {
        if (running)
            _ctx.RoomObject.ExtraData.UpdateSection(RUNNING_SECTION, true);
        else if (WasRunning())
            _ctx.RoomObject.ExtraData.DeleteSection(RUNNING_SECTION);
    }

    private bool WasRunning() => _ctx.RoomObject.ExtraData.TryGetSection(RUNNING_SECTION, out _);

    // Due at the boundary itself: a delay computed from a second clock read could land 1 ms late.
    private void Schedule() =>
        TimerSystem.ScheduleAt(_ctx.ObjectId, _nextHalfSecondAtMs, TickAsync);

    private async Task TickAsync(CancellationToken ct)
    {
        if (!_isRunning)
            return;

        if (_halfSeconds >= WiredConfig.ClockMaxHalfSeconds)
        {
            await StopAsync();

            return;
        }

        _halfSeconds++;

        await PublishAsync(ct, reached: true);

        _nextHalfSecondAtMs += WiredPulses.MS;

        Schedule();
    }

    /// <summary>
    /// Shows whole seconds on the item and tells wired the clock moved. The time is written
    /// without the state-change event: a counter set to 30 by Adjust Counter did not fire
    /// "Furni state changes" (official client capture; the tick is assumed to behave the same).
    /// Only a counted tick <paramref name="reached"/> its time: Clock Reaches Time 0:30 did not
    /// fire when Adjust Clock set the counter to 0:30 (4/4) or added 5 s at 0:29 (2/2), and fired
    /// on the counted 0:30 (1/1) (official client capture 2026-10-24, trigger.15.R5). Inference:
    /// a reset is no tick either.
    /// </summary>
    private async Task PublishAsync(CancellationToken ct, bool reached)
    {
        var seconds = _halfSeconds / 2;

        if (seconds != GetState())
        {
            StuffData.SetState(seconds.ToString(CultureInfo.InvariantCulture));
            // A tick goes out on its own; a change wired makes joins the tick's batch.
            PersistStuffData(!WiredSystem.QueueFloorState(_ctx.ObjectId));
        }

        await _ctx.PublishRoomEventAsync(
            new WiredClockTickEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                ObjectId = _ctx.ObjectId,
                HalfSeconds = _halfSeconds,
                IsCounted = reached,
            },
            ct
        );
    }
}
