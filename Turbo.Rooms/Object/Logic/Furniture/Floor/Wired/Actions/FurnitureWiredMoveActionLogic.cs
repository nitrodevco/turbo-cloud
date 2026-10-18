using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// An action that moves furni. It works its moves out first (<see cref="PlanMoves"/>) and makes
/// them after, so a stack with "Can Perform Movements" can check every move its effects are about
/// to make, in the order they will run, before any of them happens; the moves then made are the
/// ones checked, random rolls included. Without that condition an action plans and moves at once.
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

    /// <summary>Whether the action counts as having run even when no furni moved.</summary>
    protected virtual bool SucceedsWithoutMoving => false;

    /// <summary>
    /// Plans the furni in slot 0 onto the first furni in slot 1, offset by
    /// (<paramref name="dx"/>, <paramref name="dy"/>) tiles. "Furni to furni" is this with no
    /// offset and "move furni to" with one; they were the same loop written twice.
    /// </summary>
    protected void PlanOntoTargetFurni(IWiredContext ctx, WiredMovePlan plan, int dx, int dy)
    {
        var movers = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (movers.Count == 0 || targets.Count == 0)
            return;

        var target = targets[0];
        var at = plan.Where(target);

        foreach (var mover in movers)
        {
            if (mover.ObjectId == target.ObjectId)
                continue;

            plan.Move(mover, at.X + dx, at.Y + dy);
        }
    }

    internal WiredMovePlan Plan(IWiredContext ctx, WiredGhostLayout layout)
    {
        var plan = new WiredMovePlan(layout, ctx.Policy, MapModule);

        PlanMoves(ctx, plan);

        return plan;
    }

    internal async Task<bool> ApplyAsync(IWiredExecutionContext ctx, WiredMovePlan plan) =>
        await plan.ApplyAsync(ctx) || SucceedsWithoutMoving;

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        ApplyAsync(ctx, Plan(ctx, new WiredGhostLayout(MapModule, FurniModule)));
}
