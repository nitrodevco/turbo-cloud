using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the furni of the first slot next to the furni of the second slot: N tiles away from
/// the target in the given cardinal direction. Params: direction (0, 2, 4 or 6) and tiles 1-5.
/// </summary>
[RoomObjectLogic("wf_act_move_furni_to")]
public class WiredActionMoveFurniTo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_FURNI_TO;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<Rotation>(
                Rotation.North,
                Rotation.North,
                Rotation.East,
                Rotation.South,
                Rotation.West
            ),
            new WiredRangeParamRule(1, 5, 1),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
            [WiredFurniSourceType.SelectedItems],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var movers = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (movers.Count == 0 || targets.Count == 0)
            return false;

        var direction = GetIntParamOrDefault(0, Rotation.North);
        var tiles = GetIntParamOrDefault(1, 1);
        var map = _roomGrain.MapModule;
        var actionCtx = ctx.AsActionContext();
        var (dx, dy) = map.GetDirectionOffset(direction);
        var target = targets[0];
        var moved = false;

        foreach (var mover in movers)
        {
            if (mover.ObjectId == target.ObjectId)
                continue;

            var x = target.X + dx * tiles;
            var y = target.Y + dy * tiles;

            if (!map.InBounds(x, y))
                continue;

            if (
                !await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    actionCtx,
                    mover.ObjectId,
                    x,
                    y,
                    mover.Rotation
                )
            )
                continue;

            moved |= await ctx.ProcessFloorItemMovementAsync(mover, map.ToIdx(x, y), null, null);
        }

        return moved;
    }
}
