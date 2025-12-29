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

[RoomObjectLogic("wf_slc_furni_area")]
public class WiredSelectorItemsInArea(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredSelectorLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx),
        IWiredSelector
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_IN_AREA;

    private readonly HashSet<int> _tileIds = [];

    private int _maxAreaSize = 100;

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            // filter the furni

            output.SelectedFurniIds.Add(id);
        }

        return output;
    }

    protected override void FillInternalData()
    {
        base.FillInternalData();

        _tileIds.Clear();

        var rootX = WiredData.IntParams[0];
        var rootY = WiredData.IntParams[1];
        var width = WiredData.IntParams[2];
        var height = WiredData.IntParams[3];
        var size = 0;
        var filled = false;

        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                if (size >= _maxAreaSize)
                {
                    filled = true;

                    break;
                }

                var x = rootX + dx;
                var y = rootY + dy;

                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;

                var tileId = (y * width) + x;

                _tileIds.Add(tileId);

                size++;
            }

            if (filled)
                break;
        }
    }
}
