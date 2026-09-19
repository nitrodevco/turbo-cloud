using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True while the room wired timer has run for less than the given pulses.</summary>
[RoomObjectLogic("wf_cnd_time_less_than")]
public class WiredConditionTimerLessThan(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TIME_ELAPSED_LESS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(1, 1200, 1)];

    protected override bool EvaluateCore(IWiredProcessingContext ctx) =>
        _roomGrain.WiredSystem.GetElapsedTimerPulses(_roomGrain.NowMs())
        < GetIntParamOrDefault(0, 1);
}
