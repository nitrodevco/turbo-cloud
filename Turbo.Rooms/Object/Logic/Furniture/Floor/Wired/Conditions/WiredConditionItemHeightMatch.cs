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

/// <summary>
/// Compares the altitude of the picked furni (their own z, in hundredths) with the three-way radio,
/// all or any of them as the furni quantifier says; with none picked "all" passes and "any" fails
/// (official client capture 2026-10-24, W1 lab condition.36.R1 6/6, R5 9/9; the editor opens with
/// quantifierType 1, 2026-10-20T17-37-19).
/// </summary>
[RoomObjectLogic("wf_cnd_has_altitude")]
public class WiredConditionItemHeightMatch(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.FURNI_HAS_ALTITUDE;

    protected override bool AllowsWallFurni => true;

    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Furni;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 8000, 0), new WiredRangeParamRule(0, 2, 0)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var altitude = GetIntParamOrDefault(0, 0);
        var comparison = GetIntParamOrDefault(1, 0);
        var items = GetFloorItems(ctx.GetSelection(this));

        return Quantify(
            items.Select(x => WiredComparison.CompareThreeWay(comparison, x.Z.ToInt(), altitude)),
            QuantifierRequiresAll
        );
    }
}
