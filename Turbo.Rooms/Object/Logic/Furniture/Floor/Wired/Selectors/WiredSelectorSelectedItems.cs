using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// "Furni Picks": selects the furni picked in the box. It has no input sources; its editor always
/// picks furni (AS3 1318 wired_setup/selectors/_-412.as forceFurniSelection, official form
/// contract with no sources).
/// </summary>
[RoomObjectLogic("wf_slc_furni_picks")]
public class WiredSelectorSelectedItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.SELECTED_FURNIS;

    protected override bool AllowsWallFurni => true;

    public override bool SupportsAdvancedMode() => false;

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        foreach (var id in GetStuffIds())
        {
            if (FurniModule.TryGetItem(id, out var item))
                output.SelectedFurniIds.Add((int)item.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
