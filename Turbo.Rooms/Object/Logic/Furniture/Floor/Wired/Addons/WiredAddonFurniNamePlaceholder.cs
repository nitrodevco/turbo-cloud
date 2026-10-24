using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Replaces "$(name)" in the text of the stack actions with the selected furni's names: the
/// furnidata public name ("Small Block"), as official Habbo sends it (wired matrix W3
/// action.7.X2), or the class name for furni the hotel's furnidata has no name for.
/// </summary>
[RoomObjectLogic("wf_xtra_text_output_furni_name")]
public class WiredAddonFurniNamePlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredNamePlaceholderLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.FURNI_NAME_PLACEHOLDER;

    protected override bool AllowsWallFurni => true;

    // The triggering furni first: what the placeholder named before it had a source of its own.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.SelectedItems,
            ],
        ];

    protected override List<string> GetNames(IWiredExecutionContext ctx) =>
        [
            .. GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0))
                .Select(x => x.Definition.DisplayName),
        ];
}
