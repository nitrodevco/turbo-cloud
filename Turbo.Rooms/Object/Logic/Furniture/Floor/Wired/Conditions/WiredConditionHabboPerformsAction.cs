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

/// <summary>True when the triggering users are in the state of the action in param 0.</summary>
[RoomObjectLogic("wf_cnd_user_performs_action")]
public class WiredConditionHabboPerformsAction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.PERFORMING_ACTION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredAvatarActionType>(WiredAvatarActionType.Sit)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var action = GetIntParamOrDefault(0, WiredAvatarActionType.Sit);
        var players = GetPlayers(ctx.GetSelection(this));

        return Quantify(
            players.Select(p =>
                WiredAvatarActionMatcher.IsPerforming(p, action, _wiredData.StringParam)
            ),
            true
        );
    }
}
