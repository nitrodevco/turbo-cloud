using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// What the variable share and import add-ons have in common (AS3 <c>addons/_-226</c>,
/// WIN63-202610201032): three int params, one per "Categories" checkbox - text connectors,
/// subvariable creators, variable FX - 1 for ticked.
/// </summary>
public abstract class WiredAddonVariableSharing(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private static readonly WiredVariableAddonCategories[] CATEGORY_PARAMS =
    [
        WiredVariableAddonCategories.TextConnectors,
        WiredVariableAddonCategories.SubVariableCreators,
        WiredVariableAddonCategories.VariableFx,
    ];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
        ];

    /// <summary>The ticked categories.</summary>
    public WiredVariableAddonCategories Categories
    {
        get
        {
            var categories = WiredVariableAddonCategories.None;

            for (var i = 0; i < CATEGORY_PARAMS.Length; i++)
            {
                if (GetIntParamOrDefault(i, false))
                    categories |= CATEGORY_PARAMS[i];
            }

            return categories;
        }
    }
}
