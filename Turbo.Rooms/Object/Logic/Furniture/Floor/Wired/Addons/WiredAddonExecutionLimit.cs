using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Caps how often the stack fires: at most N times (param 0) per time window (param 1, in
/// half-second pulses). Firings past the cap are dropped until the window rolls over.
/// </summary>
[RoomObjectLogic("wf_xtra_execution_limit")]
public class WiredAddonExecutionLimit(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private long _windowStartMs;
    private int _executionsInWindow;

    public override int WiredCode => (int)WiredAddonType.EXECUTION_LIMIT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(1, 100, 1), new WiredRangeParamRule(1, 20, 1)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var now = _roomGrain.NowMs();
        var windowMs = WiredPulses.ToMs(GetIntParamOrDefault(1, 1));

        if (now - _windowStartMs >= windowMs)
        {
            _windowStartMs = now;
            _executionsInWindow = 0;
        }

        if (_executionsInWindow >= GetIntParamOrDefault(0, 1))
            return Task.FromResult(false);

        _executionsInWindow++;

        return Task.FromResult(true);
    }
}
