using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_chase")]
public class WiredActionChaseHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // How far a furni notices a player, and how close counts as having caught one. The client
    // has no setting for either; they are the classic chase behaviour.
    private const int CHASE_RANGE = 3;
    private const int COLLISION_RANGE = 1;
    private const int NO_TILE = -1;

    public override int WiredCode => (int)WiredActionType.CHASE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var map = _roomGrain.MapModule;

        foreach (var floorItem in GetFloorItems(ctx.GetSelection(this)))
        {
            try
            {
                var floorIdx = map.ToIdx(floorItem.X, floorItem.Y);
                var targetIdx = NO_TILE;

                if (
                    _roomGrain.AvatarModule.TryGetNearestPlayer(
                        floorIdx,
                        CHASE_RANGE,
                        out var player,
                        out var distance
                    )
                )
                {
                    if (distance <= COLLISION_RANGE)
                    {
                        PublishCollision(floorItem, player);

                        continue;
                    }

                    // Which tile lies toward the player is the map's to say; this box only
                    // decides that the furni takes the first of them.
                    targetIdx = map.GetStepsToward(floorIdx, map.ToIdx(player.X, player.Y))
                        .DefaultIfEmpty(NO_TILE)
                        .First();
                }
                else if (
                    map.TryGetTileInFront(
                        floorIdx,
                        RotationExtensions.CARDINAL[Random.Shared.Next(0, 4)],
                        out var wanderIdx
                    )
                )
                {
                    // Nobody near: wander one tile in a random straight direction.
                    targetIdx = wanderIdx;
                }

                if (targetIdx == NO_TILE)
                    continue;

                var (targetX, targetY) = map.GetTileXY(targetIdx);

                if (
                    await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                        ActionContext.Wired,
                        floorItem.ObjectId,
                        targetX,
                        targetY,
                        floorItem.Rotation
                    )
                )
                    await ctx.ProcessFloorItemMovementAsync(
                        floorItem,
                        targetIdx,
                        floorItem.Z,
                        floorItem.Rotation
                    );
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);

                continue;
            }
        }

        return true;
    }
}
