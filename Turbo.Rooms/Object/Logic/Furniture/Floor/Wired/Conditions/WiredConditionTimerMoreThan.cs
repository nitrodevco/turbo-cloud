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
/// True once the room wired timer has run for the given pulses: the pulse the time is reached
/// passes this and Less Than both (see <see cref="WiredConditionTimerLessThan"/>).
/// </summary>
[RoomObjectLogic("wf_cnd_time_more_than")]
public class WiredConditionTimerMoreThan(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TIME_ELAPSED_MORE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        // Flash saves the slider (1 to 1200 half seconds) plus one pulse, so 2 to 1201; the
        // server stores 1 and 1202 as sent (official client capture 2026-10-24, condition.3.E10).
        [new WiredRangeParamRule(1, int.MaxValue, 20)];

    protected override bool EvaluateCore(IWiredProcessingContext ctx) =>
        WiredSystem.GetElapsedTimerPulses(NowMs()) >= GetIntParamOrDefault(0, 20);
}
