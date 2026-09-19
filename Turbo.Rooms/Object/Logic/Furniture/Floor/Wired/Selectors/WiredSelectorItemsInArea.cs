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
/// Picks the floor furni inside a rectangle. Params: root x, root y, width, height. Inverting
/// is the wired system's job, like for every selector.
/// </summary>
[RoomObjectLogic("wf_slc_furni_area")]
public class WiredSelectorItemsInArea(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_IN_AREA;

    public override List<IWiredParamRule> GetIntParamRules() =>
        WiredArea.GetParamRules(_roomGrain._wiredConfig);

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var area = WiredArea.Create(
            _roomGrain,
            GetIntParamOrDefault(0, 0),
            GetIntParamOrDefault(1, 0),
            GetIntParamOrDefault(2, 0),
            GetIntParamOrDefault(3, 0)
        );

        foreach (var tileId in area.GetTileIds(_roomGrain.MapModule.Width))
        {
            foreach (var item in _roomGrain.FurniModule.GetFloorItemsOnTile(tileId))
                output.SelectedFurniIds.Add((int)item.ObjectId);
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }
}
