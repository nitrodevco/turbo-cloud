using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Puts furni back the way they were when the box was saved. The four params pick what to
/// restore: state, direction, position, altitude. The furni come from the box's source (picked,
/// triggered, selector or signal, as Habbo offers it: allowedFurniSources [[0, 100, 200, 201]]);
/// furni in the snapshot get their own entry, and when the snapshot holds a single furni, every
/// other furni takes that furni's state (recorded on hhus, WIN63-202610121709: one tile
/// snapshotted in state 3 turns every tile a selector picks to 3).
/// </summary>
[RoomObjectLogic("wf_act_match_to_sshot")]
public class WiredActionMatchToSnapshot(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SET_FURNI_STATE;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // state
            new WiredBoolParamRule(false), // direction
            new WiredBoolParamRule(false), // position
            new WiredBoolParamRule(false), // altitude
            new WiredClampParamRule(0, 0, 0),
        ];

    // The official server opens a fresh box with a fifth int, 1 (defaults [0,0,0,0,1],
    // 2026-10-20T17-39-35-Open-wait), that the editor neither reads nor writes (AS3 actiontypes/
    // _-wy.as:49-66: four checkboxes); a save of the four stores it as 0 (official client capture
    // 2026-10-24, W1 lab action.3.E6, 2/2), and so does a save sending it as 1 (W2 lab
    // action.3.E13, 2/2). Neither value changed a restore (action.3.B2).
    private const int EDITOR_PARAMS = 4;

    public override List<int> GetDefaultIntParams() => [0, 0, 0, 0, 1];

    protected override int GetRequiredIntParamCount() => EDITOR_PARAMS;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool KeepsFurniSnapshot => true;

    protected override bool KeepsWallSnapshot => true;

    // The snapshot in the editor's text, the parts this box leaves out as "N" (official client
    // capture 2026-10-24).
    protected override string GetEditorStringParam() =>
        FormatFurniSnapshot(
            GetIntParamOrDefault(0, false),
            GetIntParamOrDefault(1, false),
            GetIntParamOrDefault(2, false),
            GetIntParamOrDefault(3, false)
        );

    protected override bool SucceedsWithoutMoving => true;

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var snapshot = GetFurniSnapshot();
        var restoreState = GetIntParamOrDefault(0, false);
        var restoreRotation = GetIntParamOrDefault(1, false);
        var restorePosition = GetIntParamOrDefault(2, false);
        var restoreAltitude = GetIntParamOrDefault(3, false);

        // A furni outside a one-furni snapshot takes that furni's state; nothing else of it was
        // recorded, so direction, position and altitude stay with furni of the snapshot itself.
        WiredFurniSnapshotEntry? onlyEntry = snapshot.Count == 1 ? snapshot.Values.First() : null;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (!snapshot.TryGetValue(item.ObjectId, out var entry))
            {
                if (restoreState && onlyEntry is { } only && item.Logic.GetState() != only.State)
                    plan.Then(execution => execution.ProcessItemStateUpdateAsync(item, only.State));

                continue;
            }

            if (restoreState && item.Logic.GetState() != entry.State)
                plan.Then(execution => execution.ProcessItemStateUpdateAsync(item, entry.State));

            var at = plan.Where(item);
            var targetX = restorePosition ? entry.X : at.X;
            var targetY = restorePosition ? entry.Y : at.Y;
            var targetRotation = restoreRotation ? (Rotation)entry.Rotation : at.Rotation;
            Altitude? targetZ = restoreAltitude ? Altitude.FromInt(entry.Z) : null;

            if (
                targetX == at.X
                && targetY == at.Y
                && targetRotation == at.Rotation
                && (targetZ is null || targetZ == at.Z)
            )
                continue;

            if (IsBlockedByUser(ctx, item, at, targetX, targetY, targetRotation))
            {
                plan.Block();

                continue;
            }

            plan.Move(item, targetX, targetY, targetZ, targetRotation);
        }

        PlanWallItems(ctx, plan, restoreState, restoreRotation, restorePosition);
    }

    /// <summary>
    /// A picked wall item goes back to where it was saved, sent as a wall move every firing (also
    /// when it is there already), and its state with it, sent every firing too (official client
    /// capture 2026-10-24T17-49-51, W1 lab action.3.B11, [1,1,1,0,1], 2/2: 7,11 back to 4,8
    /// offsets 0,89, then 4,8 to 4,8). Inferred: a saved wall side the item is no longer on is
    /// turned back as Move and Rotate turns one.
    /// </summary>
    private void PlanWallItems(
        IWiredContext ctx,
        WiredMovePlan plan,
        bool restoreState,
        bool restoreRotation,
        bool restorePosition
    )
    {
        var snapshot = GetWallSnapshot();

        foreach (var wallItem in GetWallItems(ctx.GetSelection(this)))
        {
            if (!snapshot.TryGetValue(wallItem.ObjectId, out var saved))
                continue;

            var to = WiredWallMoves.At(wallItem);

            if (restorePosition)
                to = to with
                {
                    X = saved.X,
                    Y = saved.Y,
                    WallOffset = saved.WallOffset,
                    Z = saved.Height,
                };

            if (restoreRotation && (Rotation)saved.Rotation != to.Rotation)
                plan.TurnWallItem(
                    wallItem,
                    restorePosition
                        ? to with
                        {
                            Rotation = (Rotation)saved.Rotation,
                        }
                        : WiredWallMoves.Turned(to)
                );
            else if (restorePosition)
                plan.MoveWallItem(wallItem, to);

            if (restoreState)
                plan.Then(async execution =>
                {
                    if (wallItem.Logic.GetState() != saved.State)
                        await execution.ProcessItemStateUpdateAsync(wallItem, saved.State);
                    else
                        (execution as WiredExecutionContext)?.AnnounceWallItemState(wallItem);
                });
        }
    }
}
