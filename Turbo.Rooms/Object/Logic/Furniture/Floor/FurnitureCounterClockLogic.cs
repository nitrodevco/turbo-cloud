using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The counter clock (wf_upcounter). The item state is the elapsed time in seconds, which the
/// client renders as MM:SS. A use toggles running; the client "reset" tag sends state 2.
/// Wired controls it through the clock actions and reads it through the clock trigger and
/// condition, which receive a tick event every half second while it runs.
/// </summary>
public class FurnitureCounterClockLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int USE_RESET = 2;

    private int _halfSeconds;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    /// <summary>Elapsed time in half seconds, the unit the wired clock boxes compare in.</summary>
    public int HalfSeconds => _halfSeconds;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanToggle() => false;

    public override Task OnAttachAsync(CancellationToken ct)
    {
        _halfSeconds = Math.Max(0, GetState()) * 2;

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
                await ControlAsync(WiredClockControlType.Toggle, ct);
                break;
        }
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
        _isRunning = false;

        return base.OnPickupAsync(ctx, ct);
    }

    public Task ControlAsync(WiredClockControlType control, CancellationToken ct) =>
        control switch
        {
            WiredClockControlType.Start => StartAsync(),
            WiredClockControlType.Stop => StopAsync(),
            WiredClockControlType.Reset => ResetAsync(ct),
            WiredClockControlType.Restart => RestartAsync(ct),
            WiredClockControlType.Toggle => _isRunning ? StopAsync() : StartAsync(),
            _ => Task.CompletedTask,
        };

    /// <summary>Sets, adds or subtracts time; the display follows on the next second.</summary>
    public Task AdjustAsync(WiredOperatorType op, int halfSeconds, CancellationToken ct)
    {
        var next = op switch
        {
            WiredOperatorType.Add => _halfSeconds + halfSeconds,
            WiredOperatorType.Subtract => _halfSeconds - halfSeconds,
            _ => halfSeconds,
        };

        _halfSeconds = Math.Clamp(next, 0, _roomGrain._roomConfig.WiredClockMaxHalfSeconds);

        return PublishAsync(ct);
    }

    private Task StartAsync()
    {
        if (_isRunning)
            return Task.CompletedTask;

        _isRunning = true;

        Schedule();

        return Task.CompletedTask;
    }

    private Task StopAsync()
    {
        _isRunning = false;

        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return Task.CompletedTask;
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        await StopAsync();

        _halfSeconds = 0;

        await PublishAsync(ct);
    }

    private async Task RestartAsync(CancellationToken ct)
    {
        _halfSeconds = 0;

        await PublishAsync(ct);
        await StartAsync();
    }

    private void Schedule() =>
        _roomGrain.TimerSystem.Schedule(_ctx.ObjectId, WiredPulses.MS, TickAsync);

    private async Task TickAsync(CancellationToken ct)
    {
        if (!_isRunning)
            return;

        if (_halfSeconds >= _roomGrain._roomConfig.WiredClockMaxHalfSeconds)
        {
            await StopAsync();

            return;
        }

        _halfSeconds++;

        await PublishAsync(ct);

        Schedule();
    }

    /// <summary>Shows whole seconds on the item and tells wired the clock moved.</summary>
    private async Task PublishAsync(CancellationToken ct)
    {
        var seconds = _halfSeconds / 2;

        if (seconds != GetState())
            await SetStateAsync(seconds);

        await _ctx.PublishRoomEventAsync(
            new WiredClockTickEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                ObjectId = _ctx.ObjectId,
                HalfSeconds = _halfSeconds,
            },
            ct
        );
    }
}
