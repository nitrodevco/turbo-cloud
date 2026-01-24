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

[RoomObjectLogic("wf_slc_furni_bytype")]
public class WiredSelectorItemsByType(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_BY_TYPE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var allowedDefinitionIds = new List<int>();
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            try
            {
                if (!_roomGrain._state.ItemsById.TryGetValue(id, out var item))
                    continue;

                allowedDefinitionIds.Add(item.Definition.Id);
            }
            catch
            {
                continue;
            }
        }

        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (allowedDefinitionIds.Contains(item.Definition.Id))
                output.SelectedFurniIds.Add(item.ObjectId.Value);
        }

        return output;
    }
}
