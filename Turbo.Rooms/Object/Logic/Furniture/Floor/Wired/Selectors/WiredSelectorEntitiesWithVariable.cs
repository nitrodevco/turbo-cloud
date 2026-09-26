using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks the users holding the chosen variable, optionally filtered by value: players, pets and
/// bots, by room index, as every user variable is keyed.
/// </summary>
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
}
