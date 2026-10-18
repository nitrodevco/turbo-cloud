using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_chase")]
public class WiredActionChaseHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // How close counts as having caught a player: the furni never moves when adjacent (official
    // client capture 2026-10-16). How far it notices one is WiredConfig.ChaseRange.
    private const int COLLISION_RANGE = 1;
    private const int NO_TILE = -1;

    public override int WiredCode => (int)WiredActionType.CHASE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool SucceedsWithoutMoving => true;

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var map = MapModule;
        var range = _roomGrain._wiredConfig.ChaseRange;

        foreach (var floorItem in GetFloorItems(ctx.GetSelection(this)))
        {
            var at = plan.Where(floorItem);
            var floorIdx = map.ToIdx(at.X, at.Y);
            var targetIdx = NO_TILE;

            if (
                AvatarModule.TryGetNearestPlayer(
                    floorIdx,
                    range > 0 ? range : int.MaxValue,
                    out var player,
                    out var distance
                )
            )
            {
                if (distance <= COLLISION_RANGE)
                {
                    plan.Then(() => PublishCollision(floorItem, player));

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
            {
                plan.Block();

                continue;
            }

            var (targetX, targetY) = map.GetTileXY(targetIdx);

            // Height follows the room's move physics like every other mover (the KeepAltitude
            // addon keeps it); this box used to pin it, where its mirror, flee, did not.
            plan.Move(floorItem, targetX, targetY);
        }
    }
}
