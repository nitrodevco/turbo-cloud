using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_furni_altitude")]
public class WiredSelectorItemsWithAltitude(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_WITH_ALTITUDE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 8000, 0),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.LessThan),
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var altitude = Altitude.FromInt(_wiredData.GetIntParam<int>(0));
        var output = new WiredSelectionSet();

        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (item is not IRoomFloorItem floorItem)
                continue;

            switch (_wiredData.GetIntParam<WiredComparisonType>(1))
            {
                case WiredComparisonType.LessThan:
                    if (floorItem.Z < altitude)
                        output.SelectedFurniIds.Add(item.ObjectId.Value);
                    continue;
                case WiredComparisonType.GreaterThan:
                    if (floorItem.Z > altitude)
                        output.SelectedFurniIds.Add(item.ObjectId.Value);
                    continue;
                case WiredComparisonType.Equals:
                    if (floorItem.Z == altitude)
                        output.SelectedFurniIds.Add(item.ObjectId.Value);
                    continue;
            }
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }
}
