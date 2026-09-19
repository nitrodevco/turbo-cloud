using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the players holding the chosen variable, optionally filtered by value.</summary>
[RoomObjectLogic("wf_slc_users_with_var")]
public class WiredSelectorEntitiesWithVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredSelectorItemsWithVariable(grainFactory, stuffDataFactory, ctx)
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;

    public override int WiredCode => (int)WiredSelectorType.USERS_WITH_VARIABLE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    protected override IEnumerable<int> EnumerateTargets()
    {
        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
        {
            if (avatar is IRoomPlayer player)
                yield return player.PlayerId;
        }
    }
}
