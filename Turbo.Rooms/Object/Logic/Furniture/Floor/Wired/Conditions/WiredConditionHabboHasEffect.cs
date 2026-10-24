using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when the triggering avatars wear the effect id in param 0 (zero: any effect).</summary>
[RoomObjectLogic("wf_cnd_wearing_effect")]
public class WiredConditionHabboHasEffect(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_IS_WEARING_EFFECT;

    // The editor's effect id is a number input over the whole int range
    // (NumberInputParam(0, -2147483648, 2147483647), conditions/_-w1h.as:38, AS3
    // WIN63-202610201032), so any id it sends is kept; one nobody wears matches nobody.
    public override List<IWiredParamRule> GetIntParamRules() => [WiredRules.AnyInt()];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var effectId = GetIntParamOrDefault(0, 0);
        var avatars = GetAvatars(ctx.GetSelection(this));

        return Quantify(
            avatars.Select(x => effectId == 0 ? x.EffectId != 0 : x.EffectId == effectId),
            true
        );
    }
}
