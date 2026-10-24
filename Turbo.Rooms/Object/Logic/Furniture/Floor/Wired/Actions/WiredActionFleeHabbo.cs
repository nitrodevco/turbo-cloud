using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni one tile away from the nearest player within reach (WiredConfig
/// FleeRange, ChaseFleeInLineOnly). With nobody in reach it keeps going the way it last fled
/// (east if it never fled), turning left when that way is blocked; an adjacent player is caught: no move, the collision
/// trigger fires (official client capture 2026-10-24, W1 lab: away from a player 2 tiles off
/// 8/8, the last way N 5/5 then E 10/10, also after the furni was moved by hand; blocked E ->
/// N 5/5, left or away from a far player not separable; adjacent 3/3).
/// </summary>
[RoomObjectLogic("wf_act_flee")]
public class WiredActionFleeHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int COLLISION_RANGE = 1;

    private readonly Dictionary<RoomObjectId, Rotation> _headingByItemId = [];

    public override int WiredCode => (int)WiredActionType.FLEE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _headingByItemId.Clear();
    }

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var selection = ctx.GetSelection(this);
        var map = MapModule;
        var range = WiredConfig.FleeRange;

        foreach (var item in GetFloorItems(selection))
        {
            var at = plan.Where(item);
            var itemIdx = map.ToIdx(at.X, at.Y);
            List<int> candidates;

            if (
                AvatarModule.TryGetNearestPlayer(
                    itemIdx,
                    range > 0 ? range : int.MaxValue,
                    out var nearest,
                    out var distance,
                    WiredConfig.ChaseFleeInLineOnly
                )
            )
            {
                if (distance == COLLISION_RANGE)
                {
                    plan.Then(() => PublishCollision(item, nearest));

                    continue;
                }

                // Which tiles lead away from the player is the map's to say. A player standing on
                // the furni's own tile leaves no direction, so the furni sidesteps at random.
                candidates = [.. map.GetStepsAwayFrom(itemIdx, map.ToIdx(nearest.X, nearest.Y))];

                if (candidates.Count == 0)
                {
                    var sideX = at.X + Random.Shared.Next(-1, 2);

                    if (map.InBounds(sideX, at.Y))
                        candidates.Add(map.ToIdx(sideX, at.Y));
                }
            }
            else
            {
                // The way it last fled, then each way a quarter turn further left. A furni that
                // never fled heads east (+x): a fresh box on a fresh block moved E first with
                // nobody in line (official client capture 2026-10-24, W1 lab action.12.R11,
                // 3/3 first moves, 11/11 moves).
                var heading = _headingByItemId.GetValueOrDefault(item.ObjectId, Rotation.East);

                candidates = [];

                for (var turn = 0; turn < 4; turn++)
                {
                    if (map.TryGetTileInFront(itemIdx, heading.Rotate(-2 * turn), out var next))
                        candidates.Add(next);
                }
            }

            // Each way out is tried in turn; only finding none is a blocked move.
            var fled = candidates.FirstOrDefault(
                tileIdx =>
                {
                    var (x, y) = map.GetTileXY(tileIdx);

                    return plan.TryMove(item, x, y);
                },
                -1
            );

            if (fled < 0)
            {
                plan.Block();

                continue;
            }

            var (toX, toY) = map.GetTileXY(fled);
            var fledTo = RotationExtensions.FromPoints(at.X, at.Y, toX, toY);

            plan.Then(() => _headingByItemId[item.ObjectId] = fledTo);
        }
    }
}
