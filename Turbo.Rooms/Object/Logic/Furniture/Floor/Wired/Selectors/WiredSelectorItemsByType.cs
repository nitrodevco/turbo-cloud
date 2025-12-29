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
)
    : FurnitureWiredSelectorLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx),
        IWiredSelector
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
        IWiredContext ctx,
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
                var snapshot = await ctx.Room.GetFloorItemSnapshotByIdAsync(id, ct);

                if (snapshot is not null)
                    allowedDefinitionIds.Add(snapshot.DefinitionId);
            }
            catch
            {
                continue;
            }
        }

        foreach (var item in await ctx.Room.GetAllFloorItemSnapshotsAsync(ct))
        {
            if (allowedDefinitionIds.Contains(item.DefinitionId))
                output.SelectedFurniIds.Add(item.ObjectId.Value);
        }

        return output;
    }
}
