using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when the player count of the room is within the min and max params.</summary>
[RoomObjectLogic("wf_cnd_user_count_in")]
public class WiredConditionRoomHabboCount(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.USER_COUNT_IN;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 125, 0), new WiredRangeParamRule(0, 125, 0)];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var count = _roomGrain.AvatarModule.Avatars.Count(x => x is IRoomPlayer);
        var min = GetIntParamOrDefault(0, 0);
        var max = GetIntParamOrDefault(1, 0);

        return count >= min && count <= max;
    }
}
