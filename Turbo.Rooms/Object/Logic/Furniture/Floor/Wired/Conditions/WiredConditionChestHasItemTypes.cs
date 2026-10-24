using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// As <see cref="WiredConditionChestHasItems"/>, but the quantifier folds the item types the furni
/// of slot 0 are, each counted over all the picked chests together; the chests are slot 1 and the
/// variable reference slot 2.
/// </summary>
[RoomObjectLogic("wf_cnd_chest_has_item_type")]
public class WiredConditionChestHasItemTypes(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionChestHasItems(grainFactory, stuffDataFactory, ctx)
{
    private const int SLOT_ITEM_TYPES = 0;

    public override int WiredCode => (int)WiredConditionType.CHEST_HAS_ITEM_TYPES;

    protected override int ChestSlot => 1;

    // The furni types default to the picked furni, the chests to the secondary picks and the
    // variable reference to the triggering furni (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SecondaryItems),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    // Only furni chests hold furni types (require_furni_chests, inferred as above).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot != ChestSlot || item.Logic is FurnitureWiredChestLogic { Kind: WiredChestKind.Furni }
            ? null
            : WiredSaveErrors.REQUIRE_FURNI_CHESTS;

    // One count per item type, summed over the chests: with no type picked "all" holds, with
    // no chest picked each type counts 0 (official client capture 2026-10-24, wired matrix
    // condition.46.R1b passes "> 0" 2 of 2, R1c fails it 2 of 2, R1 an empty chest counts 0).
    protected override IEnumerable<long> Counts(
        IWiredProcessingContext ctx,
        List<FurnitureWiredChestLogic> chests
    ) =>
        GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_ITEM_TYPES))
            .Select(x => ChestItemTypes.Of(x.Definition, x.Logic.StuffData.GetSnapshot()))
            .Distinct()
            .Select(type =>
                chests.Sum(chest =>
                    (long)(chest.Summary.CountsByType.TryGetValue(type, out var count) ? count : 0)
                )
            );
}
