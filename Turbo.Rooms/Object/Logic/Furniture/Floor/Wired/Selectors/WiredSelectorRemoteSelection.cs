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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Reuses the selectors of other stacks: every selector box it picks is run here and its
/// result contributes. Param 0 chooses furni (0) or users (1); param 1 is the client filter
/// option and is kept as configured. Nested remote selectors are not followed.
/// </summary>
[RoomObjectLogic("wf_slc_remote")]
public class WiredSelectorRemoteSelection(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int SELECT_FURNI = 0;

    public override int WiredCode => (int)WiredSelectorType.REMOTE_SELECTOR;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 1, 0), WiredRules.AnyInt()];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems],
        ];

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var wantFurni = GetIntParamOrDefault(0, SELECT_FURNI) == SELECT_FURNI;

        foreach (var itemId in GetStuffIds())
        {
            if (
                !_roomGrain.FurniModule.TryGetItem(itemId, out var item)
                || item.Logic is not FurnitureWiredSelectorLogic remote
                || remote is WiredSelectorRemoteSelection
            )
                continue;

            var set = await remote.SelectAsync(ctx, ct);

            if (wantFurni)
                output.SelectedFurniIds.UnionWith(set.SelectedFurniIds);
            else
                output.SelectedPlayerIds.UnionWith(set.SelectedPlayerIds);
        }

        return output;
    }
}
