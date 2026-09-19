using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni by a fixed offset. Params: the signed horizontal and vertical tile
/// offsets, each within -20..20.
/// </summary>
[RoomObjectLogic("wf_act_rel_mov")]
public class WiredActionRelativeMove(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.RELATIVE_FURNI_MOVE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(-20, 20, 0), new WiredRangeParamRule(-20, 20, 0)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var dx = GetIntParamOrDefault(0, 0);
        var dy = GetIntParamOrDefault(1, 0);

        if (dx == 0 && dy == 0)
            return false;

        var map = _roomGrain.MapModule;
        var actionCtx = ctx.AsActionContext();
        var moved = false;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            var x = item.X + dx;
            var y = item.Y + dy;

            if (!map.InBounds(x, y))
                continue;

            if (
                !await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    actionCtx,
                    item.ObjectId,
                    x,
                    y,
                    item.Rotation
                )
            )
                continue;

            moved |= await ctx.ProcessFloorItemMovementAsync(item, map.ToIdx(x, y), null, null);
        }

        return moved;
    }
}
