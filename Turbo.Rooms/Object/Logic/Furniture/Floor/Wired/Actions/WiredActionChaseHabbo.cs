using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
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
    // How close counts as having caught a player: the furni never moves when adjacent and the
    // collision trigger fires every firing (official client capture 2026-10-16, and 2026-10-24
    // W1 lab action.11.B1 3/3). With nobody noticed, it steps at random.
    private const int COLLISION_RANGE = 1;

    // How far it notices a player, and only on its row or column: Habbo steps toward a player 2 or
    // 3 tiles away in the furni's row or column every time (16/16), and takes a random straight
    // step when the player is 4 or 5 tiles away (23 firings) or off its row and column (31
    // firings) (official client capture 2026-10-24, W1 lab action.11.R1-R3).
    internal const int NOTICE_RANGE = 3;

    public override int WiredCode => (int)WiredActionType.CHASE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool SucceedsWithoutMoving => true;

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var map = MapModule;

        foreach (var floorItem in GetFloorItems(ctx.GetSelection(this)))
        {
            var at = plan.Where(floorItem);
            var floorIdx = map.ToIdx(at.X, at.Y);
            List<int> candidates = [];

            // A bot is chased like a player: the furni stepped toward the only bot in line 3/3
            // and stayed beside it 2/2 (official client capture 2026-10-24, W1 lab action.11.B3).
            // Whether catching a bot fires the collision trigger was not recorded; it does not.
            if (
                AvatarModule.TryGetNearestAvatar(
                    floorIdx,
                    NOTICE_RANGE,
                    WiredUsers.Counts,
                    out var user,
                    out var distance,
                    inLineOnly: true
                )
            )
            {
                if (distance <= COLLISION_RANGE)
                {
                    if (user is IRoomPlayer player)
                        plan.Then(() => PublishCollision(floorItem, player));

                    continue;
                }

                // Which tiles lie toward the user is the map's to say; this box only
                // decides that the furni takes the first of them it can.
                candidates.AddRange(map.GetStepsToward(floorIdx, map.ToIdx(user.X, user.Y)));
            }

            // Nobody near, or no way toward them: a random straight step onto a tile it can
            // take. It never waits while one is free (official client capture 2026-10-24, W1
            // lab: the lamp on the step toward the user, S 3 / W 3 of 6, action.11.B1; out of
            // reach at a corner with 2 free tiles, a move every firing, action.11.R10).
            foreach (
                var direction in RotationExtensions.CARDINAL.OrderBy(_ => Random.Shared.Next())
            )
            {
                if (map.TryGetTileInFront(floorIdx, direction, out var wanderIdx))
                    candidates.Add(wanderIdx);
            }

            var moved = candidates.Any(tileIdx =>
            {
                var (x, y) = map.GetTileXY(tileIdx);

                return plan.TryMove(floorItem, x, y);
            });

            if (!moved)
                plan.Block();
        }
    }
}
