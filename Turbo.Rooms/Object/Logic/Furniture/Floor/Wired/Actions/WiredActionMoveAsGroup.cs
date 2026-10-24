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
/// The corner of the box around the group (its smallest x and smallest y) lands on the target
/// tile plus the offset, whichever furni stands there and in whatever order they were picked,
/// and every furni moves by the same delta; a furni that cannot move stays and the others still
/// move (official client capture 2026-10-24, W1 lab action.57.R3 4/4, action.57.B1/B2 2/2).
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
        var placements = group.Select(x => (Item: x, At: plan.Where(x))).ToList();
        var dx = targetX + GetIntParamOrDefault(PARAM_OFFSET_X, 0) - placements.Min(x => x.At.X);
        var dy = targetY + GetIntParamOrDefault(PARAM_OFFSET_Y, 0) - placements.Min(x => x.At.Y);

        if (dx == 0 && dy == 0)
            return;

        // The furni furthest along the way goes first, so each one steps onto a tile the one
        // before it has just left instead of being blocked by its own group. One that cannot go
        // (off the room, a user, furni in the way) stays; the rest go on.
        foreach (
            var (item, at) in placements.OrderByDescending(x =>
                x.At.X * Math.Sign(dx) + x.At.Y * Math.Sign(dy)
            )
        )
        {
            var (x, y) = (at.X + dx, at.Y + dy);

            if (!map.InBounds(x, y) || IsBlockedByUser(ctx, item, at, x, y, at.Rotation))
            {
                plan.Block();

                continue;
            }

            plan.Move(item, x, y);
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
