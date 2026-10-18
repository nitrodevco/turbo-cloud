using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>Moves the picked furni one tile away from the nearest player within reach.</summary>
[RoomObjectLogic("wf_act_flee")]
public class WiredActionFleeHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int FLEE_RANGE = 3;

    public override int WiredCode => (int)WiredActionType.FLEE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var selection = ctx.GetSelection(this);
        var map = MapModule;

        foreach (var item in GetFloorItems(selection))
        {
            var at = plan.Where(item);
            var itemIdx = map.ToIdx(at.X, at.Y);

            if (!AvatarModule.TryGetNearestPlayer(itemIdx, FLEE_RANGE, out var nearest, out _))
                continue;

            // Which tiles lead away from the player is the map's to say. A player standing on
            // the furni's own tile leaves no direction, so the furni sidesteps at random.
            var candidates = map.GetStepsAwayFrom(itemIdx, map.ToIdx(nearest.X, nearest.Y))
                .ToList();

            if (candidates.Count == 0)
            {
                var sideX = at.X + Random.Shared.Next(-1, 2);

                if (map.InBounds(sideX, at.Y))
                    candidates.Add(map.ToIdx(sideX, at.Y));
            }

            // Each way out is tried in turn; only finding none is a blocked move.
            if (
                !candidates.Any(tileIdx =>
                {
                    var (x, y) = map.GetTileXY(tileIdx);

                    return plan.TryMove(item, x, y);
                })
            )
                plan.Block();
        }
    }
}
