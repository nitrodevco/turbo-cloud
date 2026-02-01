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
using Turbo.Rooms.Wired.IntParams;

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

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntParamRule(0),
            new WiredIntParamRule(0),
            new WiredIntParamRule(0),
            new WiredIntParamRule(0),
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        foreach (var tileId in _tileIds)
        {
            try
            {
                var itemIds = _roomGrain._state.TileFloorStacks[tileId];

                foreach (var itemId in itemIds)
                    output.SelectedFurniIds.Add((int)itemId);
            }
            catch
            {
                continue;
            }
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
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
                if (size >= _roomGrain._roomConfig.WiredSelectorMaxAreaSize)
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

        await base.FillInternalDataAsync(ct);
    }
}
