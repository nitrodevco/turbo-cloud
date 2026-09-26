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
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int FLEE_RANGE = 3;

    public override int WiredCode => (int)WiredActionType.FLEE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var map = _roomGrain.MapModule;
        var moved = false;

        foreach (var item in GetFloorItems(selection))
        {
            var itemIdx = map.ToIdx(item.X, item.Y);

            if (
                !_roomGrain.AvatarModule.TryGetNearestPlayer(
                    itemIdx,
                    FLEE_RANGE,
                    out var nearest,
                    out _
                )
            )
                continue;

            // Which tiles lead away from the player is the map's to say. A player standing on
            // the furni's own tile leaves no direction, so the furni sidesteps at random.
            var candidates = map.GetStepsAwayFrom(itemIdx, map.ToIdx(nearest.X, nearest.Y))
                .ToList();

            if (candidates.Count == 0)
            {
                var sideX = item.X + Random.Shared.Next(-1, 2);

                if (map.InBounds(sideX, item.Y))
                    candidates.Add(map.ToIdx(sideX, item.Y));
            }

            foreach (var tileIdx in candidates)
            {
                var (x, y) = map.GetTileXY(tileIdx);

                if (await ctx.TryMoveFloorItemAsync(item, x, y))
                {
                    moved = true;

                    break;
                }
            }
        }

        return moved;
    }
}
