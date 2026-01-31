using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_chase")]
public class WiredActionChaseHabbo(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.CHASE;

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
        var selection = await ctx.GetEffectiveSelectionAsync(this, ct);
        var actionCtx = ctx.AsActionContext();

        foreach (var furniId in selection.SelectedFurniIds)
        {
            try
            {
                if (
                    !_roomGrain._state.ItemsById.TryGetValue(furniId, out var item)
                    || item is not IRoomFloorItem floorItem
                )
                    continue;

                var didCollide = false;
                var bestTileIdx = -1;
                var bestDistance = int.MaxValue;
                var floorIdx = _roomGrain.MapModule.ToIdx(floorItem.X, floorItem.Y);

                foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
                {
                    if (avatar is not IRoomPlayer player)
                        continue;

                    var playerIdx = _roomGrain.MapModule.ToIdx(player.X, player.Y);
                    var distance = _roomGrain.MapModule.GetDistanceBetween(floorIdx, playerIdx);

                    if (distance <= 1)
                    {
                        didCollide = true;

                        _ = _ctx.PublishRoomEventAsync(
                            new RoomItemCollisionEvent()
                            {
                                ObjectId = floorItem.ObjectId,
                                CausedBy = ActionContext.CreateForPlayer(
                                    player.PlayerId,
                                    _roomGrain.RoomId
                                ),
                                RoomId = _roomGrain.RoomId,
                            },
                            ct
                        );

                        break;
                    }

                    if (distance > 3)
                        continue;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTileIdx = playerIdx;
                    }
                }

                if (didCollide)
                    continue;

                if (bestTileIdx > -1)
                {
                    var targetIdx = GetTargetTileIdx(floorIdx, bestTileIdx);
                    var (targetX, targetY) = _roomGrain.MapModule.GetTileXY(targetIdx);

                    if (
                        await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                            ActionContext.Wired,
                            floorItem.ObjectId,
                            targetX,
                            targetY,
                            floorItem.Rotation
                        )
                    )
                    {
                        await ctx.ProcessFloorItemMovementAsync(
                            floorItem,
                            targetIdx,
                            floorItem.Z,
                            floorItem.Rotation
                        );

                        continue;
                    }
                }

                // random
                continue;
            }
            catch
            {
                continue;
            }
        }

        return true;
    }

    private int GetTargetTileIdx(int fromIdx, int toIdx)
    {
        var fx = fromIdx % _roomGrain.MapModule.Width;
        var fy = fromIdx / _roomGrain.MapModule.Width;

        var tx = toIdx % _roomGrain.MapModule.Width;
        var ty = toIdx / _roomGrain.MapModule.Width;

        var dx = tx - fx;
        var dy = ty - fy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            if (dx > 0)
                return fromIdx + 1;
            if (dx < 0)
                return fromIdx - 1;
        }

        if (dy > 0)
            return fromIdx + _roomGrain.MapModule.Width;
        if (dy < 0)
            return fromIdx - _roomGrain.MapModule.Width;

        return fromIdx;
    }
}
