using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// An action that moves furni. It works its moves out first (<see cref="PlanMoves"/>) and makes
/// them after, so "Can Perform Movements" can work out the moves of a random order of the stack's
/// effects on a ghost of the room before any of them happens. Running, an action plans and moves
/// at once.
/// </summary>
public abstract class FurnitureWiredMoveActionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    /// <summary>
    /// Works out this action's moves on <paramref name="plan"/>, reading every furni's position
    /// from it (<see cref="WiredMovePlan.Where"/>), never from the furni. Nothing happens here:
    /// what the action does besides moving goes through <see cref="WiredMovePlan.Then(System.Action)"/>.
    /// </summary>
    protected abstract void PlanMoves(IWiredContext ctx, WiredMovePlan plan);

    /// <summary>
    /// The floor picks in a new random order every firing, each moving onto the room as the ones
    /// before left it. A block and the furni on it, both picked: the block first moves both, the
    /// top one landing on it again; the top first drops it onto the free tile and the block is
    /// blocked by it. Habbo does either, never the block alone (official client capture
    /// 2026-10-24/25, W1 lab action.33.X9 Relative Move both 12/21, action.13.X9 Move To
    /// Direction both 12/21, action.4.X9 Move and Rotate both 3/6, the block listed first when
    /// both moved 16/16). That the order is what decides it is inferred: it fits every firing.
    /// </summary>
    protected List<IRoomFloorItem> GetFloorPicksInRandomOrder(IWiredSelectionSet selection)
    {
        var picks = GetFloorItems(selection);

        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(picks));

        return picks;
    }

    /// <summary>Whether the action counts as having run even when no furni moved.</summary>
    protected virtual bool SucceedsWithoutMoving => false;

    /// <summary>
    /// Plans the furni in slot 0 onto the first furni in slot 1 (with
    /// <paramref name="anyTarget"/>, one of them drawn at random), offset by
    /// (<paramref name="dx"/>, <paramref name="dy"/>) tiles. "Furni to furni" is this with no
    /// offset and "move furni to" with one; they were the same loop written twice. With
    /// <paramref name="blockedByUsers"/>, an avatar on the target tile blocks the move (see
    /// <see cref="IsBlockedByUser"/>).
    /// </summary>
    protected void PlanOntoTargetFurni(
        IWiredContext ctx,
        WiredMovePlan plan,
        int dx,
        int dy,
        bool anyTarget = false,
        bool blockedByUsers = false
    )
    {
        var movers = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (movers.Count == 0 || targets.Count == 0)
            return;

        var target = anyTarget ? targets[Random.Shared.Next(targets.Count)] : targets[0];
        var at = plan.Where(target);

        foreach (var mover in movers)
        {
            if (mover.ObjectId == target.ObjectId)
                continue;

            var moverAt = plan.Where(mover);

            if (
                blockedByUsers
                && IsBlockedByUser(ctx, mover, moverAt, at.X + dx, at.Y + dy, moverAt.Rotation)
            )
            {
                plan.Block();

                continue;
            }

            plan.Move(mover, at.X + dx, at.Y + dy);
        }
    }

    /// <summary>
    /// The wall items of <paramref name="selection"/> are announced where they are, each in the
    /// action's WiredMovements as a wall move whose source is its target: Set Altitude and Furni
    /// To Furni keep a wall pick and send that on every firing (official client capture
    /// 2026-10-24T17-49-51, WIN63-202610201032, wired matrix action.29.B11 / action.34.B11, 2/2).
    /// </summary>
    protected void PlanWallItemsInPlace(IWiredSelectionSet selection, WiredMovePlan plan)
    {
        foreach (var wallItem in GetWallItems(selection))
            plan.Then(execution =>
            {
                (execution as WiredExecutionContext)?.AnnounceWallItemInPlace(wallItem);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Moves the wall items of <paramref name="selection"/> by (<paramref name="dx"/>,
    /// <paramref name="dy"/>) tiles (<see cref="WiredWallMoves.TryStep"/>), each in the action's
    /// WiredMovements; with <paramref name="turn"/>, each also turns onto the other wall side and
    /// goes out as its own item update instead. Relative Move, Move and Rotate and Move As Group
    /// keep a wall pick and move it (official client capture 2026-10-24T17-49-51, W1 lab
    /// action.33.B11, action.4.B11, action.57.B11, 2/2 each); a wall item that does not move
    /// is still sent where it is (action.57.B11, its second firing).
    /// </summary>
    protected void PlanWallItemSteps(
        IWiredSelectionSet selection,
        WiredMovePlan plan,
        int dx,
        int dy,
        bool turn = false
    )
    {
        foreach (var wallItem in GetWallItems(selection))
        {
            if (!WiredWallMoves.TryStep(MapModule, WiredWallMoves.At(wallItem), dx, dy, out var to))
            {
                plan.Block();

                continue;
            }

            if (turn)
                plan.TurnWallItem(wallItem, WiredWallMoves.Turned(to));
            else
                plan.MoveWallItem(wallItem, to);
        }
    }

    /// <summary>
    /// Whether a user stands on the tiles a furni would move onto from <paramref name="from"/>:
    /// that blocks Move and Rotate Furni, Relative Furni Movement, Match To Snapshot and Move As
    /// Group, with no collision (official client capture 2026-10-24, W1 lab action.4.B1 4/4,
    /// action.33.X5 3/3, action.3.B2 2/2, action.57.B1 2/2). The movement physics add-on's "move
    /// through users" lets the furni through them, all or the ones it names (addon.7.R1, 1/1).
    /// A user on the furni itself is not in its way, nor is one a turn in place covers
    /// (action.4.B2b, 1/1).
    /// </summary>
    protected bool IsBlockedByUser(
        IWiredContext ctx,
        IRoomFloorItem item,
        WiredFurniPlacement from,
        int x,
        int y,
        Rotation rotation
    )
    {
        if (
            (from.X == x && from.Y == y)
            || !MapModule.GetTileIdForSize(
                x,
                y,
                rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
            return false;

        var throughUsers = ctx.Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers);
        var passable = ctx.Policy.MoveThroughUserIds;
        var onIt = AvatarModule.GetAvatarsOnItem(item, standingOnIt: false).ToHashSet();

        return tileIds.Any(idx =>
            AvatarModule
                .GetAvatarsOnTile(idx)
                .Any(avatar =>
                    !onIt.Contains(avatar)
                    && !(throughUsers && (passable?.Contains(avatar.ObjectId) ?? true))
                )
        );
    }

    internal WiredMovePlan Plan(IWiredContext ctx, WiredGhostLayout layout)
    {
        var plan = new WiredMovePlan(layout, ctx.Policy, MapModule);

        PlanMoves(ctx, plan);

        return plan;
    }

    private async Task<bool> ApplyAsync(IWiredExecutionContext ctx, WiredMovePlan plan) =>
        await plan.ApplyAsync(ctx) || SucceedsWithoutMoving;

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        ApplyAsync(ctx, Plan(ctx, new WiredGhostLayout(MapModule, FurniModule)));
}
