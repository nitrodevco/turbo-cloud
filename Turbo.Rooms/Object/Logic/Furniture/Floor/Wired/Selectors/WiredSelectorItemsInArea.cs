using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_furni_area")]
public class WiredSelectorItemsInArea(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_IN_AREA;

    private readonly HashSet<int> _tileIds = [];

    private int _maxAreaSize = 100;

    public override async Task<WiredSelectionSet> SelectAsync(
        WiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            output.SelectedFurniIds.Add(id);
        }

        foreach (var tileId in _tileIds)
        {
            try
            {
                var flooritems = ctx.Room._liveState.TileFloorStacks[tileId];

                foreach (var item in flooritems)
                    output.SelectedFurniIds.Add(item.Value);
            }
            catch
            {
                continue;
            }
        }

        return output;
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _tileIds.Clear();

        var map = await _ctx.Room.GetMapSnapshotAsync(ct);
        var rootX = WiredData.IntParams[0];
        var rootY = WiredData.IntParams[1];
        var areaW = WiredData.IntParams[2];
        var areaH = WiredData.IntParams[3];
        var mapW = map.Width;
        var mapH = map.Height;
        var size = 0;
        var filled = false;

        for (var dy = 0; dy < areaH; dy++)
        {
            for (var dx = 0; dx < areaW; dx++)
            {
                if (size >= _maxAreaSize)
                {
                    filled = true;

                    break;
                }

                var x = rootX + dx;
                var y = rootY + dy;

                if (x >= mapW || y >= mapH)
                    continue;

                var tileId = (y * mapW) + x;

                _tileIds.Add(tileId);
                size++;
            }

            if (filled)
                break;
        }
    }
}
