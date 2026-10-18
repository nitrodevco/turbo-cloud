using System;
using System.Collections.Generic;
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
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // The editor's "turn" radio, wiredfurni.params.turn.0 to .6 (AS3 MoveToDirection).
    private const int TURN_WAIT = 0;
    private const int TURN_RIGHT_45 = 1;
    private const int TURN_RIGHT_90 = 2;
    private const int TURN_LEFT_45 = 3;
    private const int TURN_LEFT_90 = 4;
    private const int TURN_BACK = 5;
    private const int TURN_RANDOM = 6;

    private readonly Dictionary<RoomObjectId, Rotation> _headingByItemId = [];

    public override int WiredCode => (int)WiredActionType.MOVE_TO_DIRECTION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 7, 0),
            new WiredRangeParamRule(0, 6, 0),
            // Blocked by users unless unticked (official client capture: defaultIntParams [0,0,1]).
            new WiredBoolParamRule(true),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _headingByItemId.Clear();
    }

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var selection = ctx.GetSelection(this);
        var startDirection = (Rotation)GetIntParamOrDefault(0, 0);
        var turnMode = GetIntParamOrDefault(1, TURN_WAIT);
        var blockOnUsers = GetIntParamOrDefault(2, false);
        var map = MapModule;

        foreach (var item in GetFloorItems(selection))
        {
            if (!_headingByItemId.TryGetValue(item.ObjectId, out var heading))
                heading = startDirection;

            var moved = false;

            // Try the heading, then the turned headings until one is free or we give up. Only
            // giving up is a blocked move: turning and going another way is the box working.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var at = plan.Where(item);

                if (
                    map.TryGetTileInFront(map.ToIdx(at.X, at.Y), heading, out var nextIdx)
                    && !IsBlockedByUser(nextIdx, blockOnUsers, item, plan)
                )
                {
                    var (x, y) = map.GetTileXY(nextIdx);

                    if (plan.TryMove(item, x, y))
                    {
                        moved = true;

                        break;
                    }
                }

                if (turnMode == TURN_WAIT)
                    break;

                heading = Turn(heading, turnMode);
            }

            if (!moved)
                plan.Block();

            var finalHeading = heading;

            plan.Then(() => _headingByItemId[item.ObjectId] = finalHeading);
        }
    }

    private bool IsBlockedByUser(
        int tileIdx,
        bool blockOnUsers,
        IRoomFloorItem item,
        WiredMovePlan plan
    )
    {
        if (!blockOnUsers || !AvatarModule.HasAvatarOnTile(tileIdx))
            return false;

        foreach (var avatar in AvatarModule.GetAvatarsOnTile(tileIdx))
        {
            if (avatar is IRoomPlayer player)
                plan.Then(() => PublishCollision(item, player));
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
            TURN_BACK => heading.Opposite(),
            _ => heading,
        };
}
