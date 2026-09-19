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

/// <summary>True when the triggering users carry the hand item in param 0 (zero: anything).</summary>
[RoomObjectLogic("wf_cnd_has_handitem")]
public class WiredConditionHabboHasHanditem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_HAS_HANDITEM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.HandItem(_roomGrain._roomConfig)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var handItemId = GetIntParamOrDefault(0, 0);
        var players = GetPlayers(ctx.GetSelection(this));

        return Quantify(
            players.Select(p => handItemId == 0 ? p.HandItemId != 0 : p.HandItemId == handItemId),
            true
        );
    }
}
