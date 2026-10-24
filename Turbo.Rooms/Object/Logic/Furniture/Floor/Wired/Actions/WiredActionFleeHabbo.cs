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
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni one tile away from the nearest player within reach on its row
/// or column (<see cref="NOTICE_RANGE"/>). With nobody in reach it keeps going the way it last fled
/// (east if it never fled), turning a random quarter left or right when that way is blocked; an
/// adjacent player is caught: no move, the collision trigger fires (official client capture
/// 2026-10-24, W1 lab: away from a player 2 tiles off 8/8, the last way N 5/5 then E 10/10, also
/// after the furni was moved by hand; blocked E at the map edge -> N 5 / S 3 of 8, whichever
/// side the player stood, never back W (action.12.B14); adjacent 3/3).
/// </summary>
[RoomObjectLogic("wf_act_flee")]
public class WiredActionFleeHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int COLLISION_RANGE = 1;

    // How far it notices a player, and only on its row or column: Habbo stepped away from a player
    // 2 tiles away in its row or column 8/8 and ignored one 4 tiles away or off them (official
    // client capture 2026-10-24, W1 lab action.12.R1-R2); 3, as Chase, is inferred.
    private const int NOTICE_RANGE = WiredActionChaseHabbo.NOTICE_RANGE;

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

        foreach (var item in GetFloorItems(selection))
        {
            var at = plan.Where(item);
            var itemIdx = map.ToIdx(at.X, at.Y);
            List<int> candidates;

            // A bot is fled from as Chase chases one (inference: only Chase was recorded with a
            // bot, WiredUsers).
            if (
                AvatarModule.TryGetNearestAvatar(
                    itemIdx,
                    NOTICE_RANGE,
                    WiredUsers.Counts,
                    out var nearest,
                    out var distance,
                    inLineOnly: true
                )
            )
            {
                if (distance == COLLISION_RANGE)
                {
                    if (nearest is IRoomPlayer player)
                        plan.Then(() => PublishCollision(item, player));

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
                // The way it last fled, then a quarter turn to a random side, then the other side,
                // then back (back is inferred: Habbo never went back while a side was open). A
                // furni that never fled heads east (+x): a fresh box on a fresh block moved E
                // first with nobody in line (official client capture 2026-10-24, W1 lab
                // action.12.R11, 3/3 first moves, 11/11 moves).
                var heading = _headingByItemId.GetValueOrDefault(item.ObjectId, Rotation.East);
                var side = Random.Shared.Next(2) == 0 ? -2 : 2;

                candidates = [];

                foreach (var turn in new[] { 0, side, -side, 4 })
                {
                    if (map.TryGetTileInFront(itemIdx, heading.Rotate(turn), out var next))
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
