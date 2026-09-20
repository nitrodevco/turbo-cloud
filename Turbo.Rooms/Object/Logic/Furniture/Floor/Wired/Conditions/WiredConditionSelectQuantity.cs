using System.Collections.Generic;
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
/// Compares how many furni and users its inputs resolve to. Params: merged flag (kept for the
/// client), the amount, and the three-way comparison.
/// </summary>
[RoomObjectLogic("wf_cnd_slc_quantity")]
public class WiredConditionSelectQuantity(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.INPUT_SOURCE_QUANTITY;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 100, 0),
            new WiredRangeParamRule(0, 2, 1),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.AllRoomItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SignalUsers,
                WiredPlayerSourceType.AllRoomUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var selection = ctx.GetSelection(this);
        var count = selection.SelectedFurniIds.Count + selection.SelectedAvatarIds.Count;

        return WiredComparison.CompareThreeWay(
            GetIntParamOrDefault(2, 1),
            count,
            GetIntParamOrDefault(1, 0)
        );
    }
}
