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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni one tile in a heading it remembers per item, turning when blocked.
/// Params: the starting direction (0-7), the turn rule on collision, and whether a user in the
/// way stops the furni where it is (else it turns away from the user as from furni); a user in
/// the way raises the collision trigger either way.
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

        foreach (var item in GetFloorPicksInRandomOrder(selection))
        {
            if (!_headingByItemId.TryGetValue(item.ObjectId, out var heading))
                heading = startDirection;

            var moved = false;
            var tried = 0;

            // Try the heading, then the turned headings until one is free or we give up. Only
            // giving up is a blocked move: turning and going another way is the box working.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var at = plan.Where(item);

                tried |= 1 << (int)heading;

                if (map.TryGetTileInFront(map.ToIdx(at.X, at.Y), heading, out var nextIdx))
                {
                    // A user ahead raises the collision trigger either way. With "block furni
                    // movements" ticked the furni stays where it is, without turning; unticked,
                    // the user is in the way like any furni and the turn rule applies (official
                    // client capture 2026-10-24, W1 action.13.B5 4/4, B6 4/4). The movement
                    // physics add-on's "move through users" outranks both: the furni goes onto
                    // the user's tile, the collision still raised (addon.7.X5, 1/1).
                    if (MeetsUser(nextIdx, item, plan) && !MovesThroughUsersAt(ctx, nextIdx))
                    {
                        if (blockOnUsers)
                            break;
                    }
                    else
                    {
                        var (x, y) = map.GetTileXY(nextIdx);

                        if (plan.TryMove(item, x, y))
                        {
                            moved = true;

                            break;
                        }
                    }
                }

                if (turnMode == TURN_WAIT)
                    break;

                heading = Turn(heading, turnMode, tried);
            }

            if (!moved)
                plan.Block();

            var finalHeading = heading;

            plan.Then(() => _headingByItemId[item.ObjectId] = finalHeading);
        }
    }

    /// <summary>Whether a user stands on the tile, raising the collision trigger for each.</summary>
    private bool MeetsUser(int tileIdx, IRoomFloorItem item, WiredMovePlan plan)
    {
        if (!AvatarModule.HasAvatarOnTile(tileIdx))
            return false;

        foreach (var avatar in AvatarModule.GetAvatarsOnTile(tileIdx))
        {
            if (WiredUsers.Counts(avatar))
                plan.Then(() => PublishCollision(item, avatar));
        }

        return true;
    }

    private bool MovesThroughUsersAt(IWiredContext ctx, int tileIdx) =>
        ctx.Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers)
        && (
            ctx.Policy.MoveThroughUserIds is not { } passable
            || AvatarModule.GetAvatarsOnTile(tileIdx).All(x => passable.Contains(x.ObjectId))
        );

    /// <summary>
    /// The next heading to try. "Random" picks one of the headings not tried yet, so a furni
    /// with a free neighbour always moves (official client capture 2026-10-24, W1 action.13.B3:
    /// moved to a free neighbour 8/8, never stayed).
    /// </summary>
    private static Rotation Turn(Rotation heading, int turnMode, int tried) =>
        turnMode switch
        {
            TURN_RIGHT_90 => heading.Rotate(2),
            TURN_LEFT_90 => heading.Rotate(-2),
            TURN_RIGHT_45 => heading.Rotate(1),
            TURN_LEFT_45 => heading.Rotate(-1),
            TURN_RANDOM => RandomUntried(heading, tried),
            TURN_BACK => heading.Opposite(),
            _ => heading,
        };

    private static Rotation RandomUntried(Rotation heading, int tried)
    {
        Span<int> untried = stackalloc int[8];
        var count = 0;

        for (var direction = 0; direction < 8; direction++)
        {
            if ((tried & (1 << direction)) == 0)
                untried[count++] = direction;
        }

        return count == 0 ? heading : (Rotation)untried[Random.Shared.Next(count)];
    }
}
