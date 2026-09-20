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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// "Remove Temporary Furni": takes the selected furni out of the room, those that
/// <c>wf_act_place_furni</c> put there and no others. A furni somebody owns is left where it is
/// however it was selected. The editor has no params.
/// </summary>
[RoomObjectLogic("wf_act_remove_furni")]
public class WiredActionRemoveFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.REMOVE_FURNI;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var actionCtx = ctx.AsActionContext();
        var removed = false;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
            removed |= await _roomGrain.FurniModule.RemoveTemporaryItemAsync(actionCtx, item, ct);

        return removed;
    }
}
