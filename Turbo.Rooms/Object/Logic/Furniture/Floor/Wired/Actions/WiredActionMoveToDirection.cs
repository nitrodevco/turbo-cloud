using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni one tile in a heading it remembers per item, turning when blocked.
/// Params: the starting direction (0-7), the turn rule on collision, and whether a user in the
/// way blocks the move (raising the collision trigger) instead of being passed.
/// </summary>
[RoomObjectLogic("wf_act_move_to_dir")]
public class WiredActionMoveToDirection(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int TURN_BACK = 0;
    private const int TURN_RIGHT_90 = 1;
    private const int TURN_LEFT_90 = 2;
    private const int TURN_RIGHT_45 = 3;
    private const int TURN_LEFT_45 = 4;
    private const int TURN_RANDOM = 5;
    private const int TURN_STOP = 6;

    private readonly Dictionary<RoomObjectId, Rotation> _headingByItemId = [];

    public override int WiredCode => (int)WiredActionType.MOVE_TO_DIRECTION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 7, 0),
            new WiredRangeParamRule(0, 6, 0),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _headingByItemId.Clear();
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var startDirection = (Rotation)GetIntParamOrDefault(0, 0);
        var turnMode = GetIntParamOrDefault(1, TURN_BACK);
        var blockOnUsers = GetIntParamOrDefault(2, false);
        var map = _roomGrain.MapModule;
        var actionCtx = ctx.AsActionContext();
        var moved = false;

        foreach (var item in GetFloorItems(selection))
        {
            if (!_headingByItemId.TryGetValue(item.ObjectId, out var heading))
                heading = startDirection;

            // Try the heading, then the turned headings until one is free or we give up.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var itemIdx = map.ToIdx(item.X, item.Y);

                if (
                    map.TryGetTileInFront(itemIdx, heading, out var nextIdx)
                    && !IsBlockedByUser(nextIdx, blockOnUsers, item)
                )
                {
                    var (x, y) = map.GetTileXY(nextIdx);

                    if (
                        await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                            actionCtx,
                            item.ObjectId,
                            x,
                            y,
                            item.Rotation
                        ) && await ctx.ProcessFloorItemMovementAsync(item, nextIdx, null, null)
                    )
                    {
                        moved = true;

                        break;
                    }
                }

                if (turnMode == TURN_STOP)
                    break;

                heading = Turn(heading, turnMode);
            }

            _headingByItemId[item.ObjectId] = heading;
        }

        return moved;
    }

    private bool IsBlockedByUser(int tileIdx, bool blockOnUsers, IRoomFloorItem item)
    {
        if (!blockOnUsers || !_roomGrain.AvatarModule.HasAvatarOnTile(tileIdx))
            return false;

        foreach (var avatar in _roomGrain.AvatarModule.GetAvatarsOnTile(tileIdx))
        {
            if (avatar is IRoomPlayer player)
                PublishCollision(item, player);
        }

        return true;
    }

    private static Rotation Turn(Rotation heading, int turnMode) =>
        turnMode switch
        {
            TURN_RIGHT_90 => heading.Rotate(2),
            TURN_LEFT_90 => heading.Rotate(-2),
            TURN_RIGHT_45 => heading.Rotate(1),
            TURN_LEFT_45 => heading.Rotate(-1),
            TURN_RANDOM => (Rotation)Random.Shared.Next(0, 8),
            _ => heading.Opposite(),
        };
}
