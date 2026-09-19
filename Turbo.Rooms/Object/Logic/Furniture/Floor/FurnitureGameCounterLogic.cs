using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The game timer (wf_game_upcounter). Its state is the remaining seconds. A use by someone
/// with rights starts a game with the room wired: scores reset, the "game starts" trigger
/// fires, the timer counts down and "game ends" fires at zero. Using it again while it runs
/// ends the game early.
/// </summary>
public class FurnitureGameCounterLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int TICK_MS = 1000;
    private const int USE_RESET = 2;

    private int _remainingSeconds;
    private bool _isRunning;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanToggle() => false;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        if (param == USE_RESET)
        {
            await StopAsync(ct);
            await SetStateAsync(_roomGrain._roomConfig.WiredGameDefaultDurationSeconds);

            return;
        }

        if (_isRunning)
        {
            await StopAsync(ct);

            return;
        }

        var duration =
            GetState() > 0 ? GetState() : _roomGrain._roomConfig.WiredGameDefaultDurationSeconds;

        _remainingSeconds = duration;
        _isRunning = true;

        await _roomGrain.WiredSystem.StartGameAsync(0, ct);

        Schedule();
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
        _isRunning = false;

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task StopAsync(CancellationToken ct)
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        await _roomGrain.WiredSystem.EndGameAsync(ct);
    }

    private void Schedule() => _roomGrain.TimerSystem.Schedule(_ctx.ObjectId, TICK_MS, TickAsync);

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
