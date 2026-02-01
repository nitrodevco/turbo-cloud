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

[RoomObjectLogic("wf_slc_furni_picks")]
public class WiredSelectorSelectedItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.SELECTED_FURNIS;

    public override bool SupportsAdvancedMode() => false;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems],
        ];

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            try
            {
                if (!_roomGrain._state.ItemsById.TryGetValue(id, out var item))
                    continue;

                output.SelectedFurniIds.Add((int)item.ObjectId);
            }
            catch
            {
                continue;
            }
        }

        return output;
    }
}
