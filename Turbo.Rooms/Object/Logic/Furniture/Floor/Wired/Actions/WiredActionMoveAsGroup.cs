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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the furni of the first slot together, keeping the layout they stand in, to a target
/// location: the furni of the second slot, or a user, plus an offset in tiles.
///
/// Int params, as the client's editor writes them: whether the target location is a user
/// (otherwise the second furni slot), offset x, offset y (-64 to 64 each).
///
/// Assumption: neither the editor nor its usage text ("the distance between the moving
/// furniture will stay consistent") says which point of the group lands on the target, so the
/// group's first furni is taken as its pivot. It lands on the target tile plus the offset and
/// every other furni moves by the same delta.
/// </summary>
[RoomObjectLogic("wf_act_move_furni_as_group")]
public class WiredActionMoveAsGroup(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_TARGET_IS_USER = 0;
    private const int PARAM_OFFSET_X = 1;
    private const int PARAM_OFFSET_Y = 2;

    // The bounds of the editor's two offset inputs.
    private const int OFFSET_MIN = -64;
    private const int OFFSET_MAX = 64;

    public override int WiredCode => (int)WiredActionType.MOVE_AS_GROUP;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(true),
            new WiredRangeParamRule(OFFSET_MIN, OFFSET_MAX, 0),
            new WiredRangeParamRule(OFFSET_MIN, OFFSET_MAX, 0),
        ];

    // The group defaults to the picked furni and the targets to the triggering furni (official
    // client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var group = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));

        if (group.Count == 0 || !TryGetTargetTile(ctx, plan, out var targetX, out var targetY))
            return;

        var map = MapModule;
        var lead = plan.Where(group[0]);
        var dx = targetX + GetIntParamOrDefault(PARAM_OFFSET_X, 0) - lead.X;
        var dy = targetY + GetIntParamOrDefault(PARAM_OFFSET_Y, 0) - lead.Y;

        if (dx == 0 && dy == 0)
            return;

        // As a group: when any of them would leave the room, none of them moves.
        if (
            group.Any(x =>
            {
                var at = plan.Where(x);

                return !map.InBounds(at.X + dx, at.Y + dy);
            })
        )
        {
            plan.Block();

            return;
        }

        // The furni furthest along the way goes first, so each one steps onto a tile the one
        // before it has just left instead of being blocked by its own group.
        foreach (
            var (item, at) in group
                .Select(x => (Item: x, At: plan.Where(x)))
                .OrderByDescending(x => x.At.X * Math.Sign(dx) + x.At.Y * Math.Sign(dy))
        )
        {
            // A blocked furni stops the ones behind it too: they would run into it.
            if (!plan.Move(item, at.X + dx, at.Y + dy))
                break;
        }
    }

    private bool TryGetTargetTile(IWiredContext ctx, WiredMovePlan plan, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (GetIntParamOrDefault(PARAM_TARGET_IS_USER, true))
        {
            var players = GetPlayers(ctx.GetSelection(this));

            if (players.Count == 0)
                return false;

            (x, y) = (players[0].X, players[0].Y);

            return true;
        }

        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (targets.Count == 0)
            return false;

        var target = plan.Where(targets[0]);

        (x, y) = (target.X, target.Y);

        return true;
    }
}
