using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Caps how often the stack fires: at most N times (param 0) per time window (param 1, in
/// half-second pulses). Firings past the cap are dropped until the window rolls over. Two limits on
/// one stack add up: [1,20] and [3,20] let 4 clicks in 10 s run and blocked the 5th, in either
/// placement order (official client capture 2026-10-24, W4 lab addon.5.R2, 4/4 + 5/5), so the box
/// is a budget the room spends from (<see cref="WiredConfig.ExecutionLimitsAddUp"/>).
/// </summary>
[RoomObjectLogic("wf_xtra_execution_limit")]
public class WiredAddonExecutionLimit(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredExecutionBudget
{
    private long _windowStartMs;
    private int _executionsInWindow;

    public override int WiredCode => (int)WiredAddonType.EXECUTION_LIMIT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        // A fresh box opens on 100 per pulse on the official server (defaults [100,1],
        // 2026-10-20T17-35-39-Open-wait).
        [new WiredRangeParamRule(1, 100, 100), new WiredRangeParamRule(1, 20, 1)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.ExecutionBudgets.Add(this);

        return Task.FromResult(true);
    }

    public bool HasRoom()
    {
        RollWindow();

        return _executionsInWindow < GetIntParamOrDefault(0, 100);
    }

    public void Spend()
    {
        RollWindow();

        _executionsInWindow++;
    }

    private void RollWindow()
    {
        var now = NowMs();

        if (now - _windowStartMs < WiredPulses.ToMs(GetIntParamOrDefault(1, 1)))
            return;

        _windowStartMs = now;
        _executionsInWindow = 0;
    }
}
