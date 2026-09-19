using System;
using System.Collections.Generic;
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
        var selection = ctx.GetSelection(this);
        var map = _roomGrain.MapModule;
        var actionCtx = ctx.AsActionContext();
        var moved = false;

        foreach (var item in GetFloorItems(selection))
        {
            var itemIdx = map.ToIdx(item.X, item.Y);
            var bestDistance = int.MaxValue;
            IRoomAvatar? nearest = null;

            foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
            {
                if (avatar is not IRoomPlayer)
                    continue;

                var distance = map.GetDistanceBetween(itemIdx, map.ToIdx(avatar.X, avatar.Y));

                if (distance <= FLEE_RANGE && distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = avatar;
                }
            }

            if (nearest is null)
                continue;

            var dx = Math.Sign(item.X - nearest.X);
            var dy = Math.Sign(item.Y - nearest.Y);
            var candidates = new List<(int x, int y)>();

            if (dx != 0)
                candidates.Add((item.X + dx, item.Y));

            if (dy != 0)
                candidates.Add((item.X, item.Y + dy));

            if (candidates.Count == 0)
                candidates.Add((item.X + Random.Shared.Next(-1, 2), item.Y));

            foreach (var (x, y) in candidates)
            {
                if (!map.InBounds(x, y))
                    continue;

                if (
                    !await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                        actionCtx,
                        item.ObjectId,
                        x,
                        y,
                        item.Rotation
                    )
                )
                    continue;

                if (await ctx.ProcessFloorItemMovementAsync(item, map.ToIdx(x, y), null, null))
                {
                    moved = true;

                    break;
                }
            }
        }

        return moved;
    }
}
