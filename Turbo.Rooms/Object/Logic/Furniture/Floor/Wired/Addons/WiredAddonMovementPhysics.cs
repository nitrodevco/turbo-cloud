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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Tunes how this stack moves things: keep altitude, move through furni, move through
/// users, blocked by furni (four checkboxes). The advanced settings say which furni can be
/// moved through (furni slot 0), which furni block (furni slot 1) and which users can be moved
/// through (user slot 0), each defaulting to the whole room (official client capture; titles
/// wiredfurni.params.sources.furni.title.physics.0/1 and sources.users.title.physics.0).
/// "Move through furni" lets a moved furni land on top of the furni of slot 0 even when nothing
/// may be stacked on them; "blocked by furni" stops it at the furni of slot 1 even when they can
/// be stacked on; furni outside the slot behave as without the add-on, and a blocked move is
/// not sent (official client capture, 2026-10-14).
/// </summary>
[RoomObjectLogic("wf_xtra_mov_physics")]
public class WiredAddonMovementPhysics(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int SLOT_THROUGH_USERS = 0;
    private const int SLOT_THROUGH_FURNI = 0;
    private const int SLOT_BLOCKING_FURNI = 1;

    public override int WiredCode => (int)WiredAddonType.MOVE_PHYSICS;

    // Keep altitude is on for a fresh box (official client capture: defaultIntParams [1,0,0,0]).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [AnyFurni, AnyFurni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users.DefaultingTo(WiredPlayerSourceType.AllRoomUsers)];

    private static WiredFurniSourceType[] AnyFurni =>
        WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.AllRoomItems);

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        // Two Movement Physics add up: keep altitude holds when either box has it, in both stack
        // orders (official client capture 2026-10-24, W1 lab addon.7.R2/R3, 7/7; both off drops).
        // The other flags are taken the same way (inference); a flag the lower box already set
        // keeps that box's furni/users.
        var had = IsFirstOfKind(ctx) ? WiredMovePhysicsFlags.None : ctx.Policy.MovePhysics;
        var flags = had;

        if (GetIntParamOrDefault(0, false))
            flags |= WiredMovePhysicsFlags.KeepAltitude;

        if (GetIntParamOrDefault(1, false) && !had.HasFlag(WiredMovePhysicsFlags.MoveThroughFurni))
        {
            flags |= WiredMovePhysicsFlags.MoveThroughFurni;
            ctx.Policy.MoveThroughFurniIds = FurniOfSlot(ctx, SLOT_THROUGH_FURNI);
        }

        if (GetIntParamOrDefault(2, false) && !had.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers))
        {
            flags |= WiredMovePhysicsFlags.MoveThroughUsers;
            ctx.Policy.MoveThroughUserIds = WiredSlotSelection
                .ForUserSlot(this, ctx, SLOT_THROUGH_USERS)
                .SelectedAvatarIds;
        }

        if (GetIntParamOrDefault(3, false) && !had.HasFlag(WiredMovePhysicsFlags.BlockedByFurni))
        {
            flags |= WiredMovePhysicsFlags.BlockedByFurni;
            ctx.Policy.BlockedByFurniIds = FurniOfSlot(ctx, SLOT_BLOCKING_FURNI);
        }

        ctx.Policy.MovePhysics = flags;

        return Task.FromResult(true);
    }

    /// <summary>The furni a furni slot names; null for "all furni in the room" (source 900, the default).</summary>
    private HashSet<int>? FurniOfSlot(IWiredProcessingContext ctx, int slot)
    {
        var sources = GetFurniSources();

        if (
            slot >= sources.Count
            || sources[slot].Length == 0
            || sources[slot].Contains(WiredFurniSourceType.AllRoomItems)
        )
            return null;

        return WiredSlotSelection.ForSlot(this, ctx, slot).SelectedFurniIds;
    }
}
