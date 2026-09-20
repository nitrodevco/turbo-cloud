using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Replaces "$name" in the text of the stack actions with the selected furni's names. The
/// server only knows a furni by its definition name (the furnidata class name); the localised
/// name the client shows lives in the client's own texts.
/// </summary>
[RoomObjectLogic("wf_xtra_text_output_furni_name")]
public class WiredAddonFurniNamePlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredNamePlaceholderLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.FURNI_NAME_PLACEHOLDER;

    protected override List<string> GetNames(IWiredSelectionSet selection) =>
        [.. GetFloorItems(selection).Select(x => x.Definition.Name)];
}
