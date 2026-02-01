using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_furni_neighborhood")]
public class WiredSelectorItemsInNeighborhood(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_IN_NEIGHBORHOOD;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.TriggeredItem, WiredFurniSourceType.SignalItems],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.SignalUsers],
        ];

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntBoolRule(false),
            new WiredIntParamRule(0),
            new WiredIntParamRule(0),
            new WiredIntParamRule(0),
        ];

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            //_inputSourceType = (WiredInputSourceType)(WiredData.IntParams?[0] ?? 0);
        }
        catch { }
    }
}
