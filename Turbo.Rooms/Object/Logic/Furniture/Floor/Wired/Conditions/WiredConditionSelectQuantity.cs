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
/// Compares how many furni or users its input resolves to. Params: what is counted (the
/// editor's merged source picker: 1 the users slot, 0 the furni slot; AS3 1318
/// conditions/_-42v.as:54-72), the amount, and the three-way comparison.
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

    // The selector's, the signal's or the triggering furni and users: no picks and no whole room
    // (official form contract of Input Source Quantity, both slots [0,200,201], default 200).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var count = GetIntParamOrDefault(0, false)
            ? WiredSlotSelection.ForUserSlot(this, ctx, 0).SelectedAvatarIds.Count
            : WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds.Count;

        return WiredComparison.CompareThreeWay(
            GetIntParamOrDefault(2, 1),
            count,
            GetIntParamOrDefault(1, 0)
        );
    }
}
