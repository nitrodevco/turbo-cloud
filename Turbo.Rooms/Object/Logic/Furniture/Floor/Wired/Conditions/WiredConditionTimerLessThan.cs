using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True while the room wired timer has run for no more than the given pulses. The pulse the time
/// is reached passes both this and More Than: [20] each, after a Reset Timers, Less passed 21
/// repeater pulses (0.27 to 10.27 s) and More from 10.27 s on (official client capture
/// 2026-10-24, wired matrix condition.4.R2 / condition.3.R2, 52 pulses).
/// </summary>
[RoomObjectLogic("wf_cnd_time_less_than")]
public class WiredConditionTimerLessThan(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TIME_ELAPSED_LESS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        // Flash saves the slider (1 to 1200 half seconds) plus one pulse, so 2 to 1201.
        [new WiredRangeParamRule(1, 1201, 20)];

    protected override bool EvaluateCore(IWiredProcessingContext ctx) =>
        WiredSystem.GetElapsedTimerPulses(NowMs()) <= GetIntParamOrDefault(0, 20);
}
