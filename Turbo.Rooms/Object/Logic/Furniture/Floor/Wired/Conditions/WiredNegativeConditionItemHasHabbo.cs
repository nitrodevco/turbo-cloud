using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_hv_avtrs")]
public class WiredNegativeConditionItemHasHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionItemHasHabbo(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_FURNIS_HAVE_AVATARS;

    public override bool IsNegative() => true;

    // Official server: a fresh "not have avatars" box opens on "one of" ([0], 2026-10-18T18-06-24-Open-wait),
    // the positive box on "all" ([1]).
    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];
}
