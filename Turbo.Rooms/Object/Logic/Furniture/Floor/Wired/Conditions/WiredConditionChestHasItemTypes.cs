using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// As <see cref="WiredConditionChestHasItems"/>, counting only furni of the types the furni of
/// slot 0 are; the chests are slot 1 and the variable reference slot 2.
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

    protected override int Count(IWiredProcessingContext ctx, FurnitureWiredChestLogic chest)
    {
        var types = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_ITEM_TYPES))
            .Select(x => ChestItemTypes.Of(x.Definition, x.Logic.StuffData.GetSnapshot()))
            .ToHashSet();

        return types.Sum(type =>
            chest.Summary.CountsByType.TryGetValue(type, out var count) ? count : 0
        );
    }
}
