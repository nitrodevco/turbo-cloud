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

/// <summary>Moves the furni of the first slot onto the tile of the furni in the second slot.</summary>
[RoomObjectLogic("wf_act_furni_to_furni")]
public class WiredActionFurniToFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_FURNI_TO_FURNI;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var movers = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (movers.Count == 0 || targets.Count == 0)
            return false;

        var target = targets[0];
        var map = _roomGrain.MapModule;
        var actionCtx = ctx.AsActionContext();
        var moved = false;

        foreach (var mover in movers)
        {
            if (mover.ObjectId == target.ObjectId)
                continue;

            if (
                !await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    actionCtx,
                    mover.ObjectId,
                    target.X,
                    target.Y,
                    mover.Rotation
                )
            )
                continue;

            moved |= await ctx.ProcessFloorItemMovementAsync(
                mover,
                map.ToIdx(target.X, target.Y),
                null,
                null
            );
        }

        return moved;
    }
}
