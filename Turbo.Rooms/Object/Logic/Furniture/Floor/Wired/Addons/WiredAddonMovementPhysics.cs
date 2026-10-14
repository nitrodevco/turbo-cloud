using System.Collections.Generic;
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
/// </summary>
[RoomObjectLogic("wf_xtra_mov_physics")]
public class WiredAddonMovementPhysics(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int SLOT_THROUGH_USERS = 0;

    public override int WiredCode => (int)WiredAddonType.MOVE_PHYSICS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
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
        var flags = WiredMovePhysicsFlags.None;

        if (GetIntParamOrDefault(0, false))
            flags |= WiredMovePhysicsFlags.KeepAltitude;

        if (GetIntParamOrDefault(1, false))
            flags |= WiredMovePhysicsFlags.MoveThroughFurni;

        if (GetIntParamOrDefault(2, false))
        {
            flags |= WiredMovePhysicsFlags.MoveThroughUsers;
            ctx.Policy.MoveThroughUserIds = WiredSlotSelection
                .ForUserSlot(this, ctx, SLOT_THROUGH_USERS)
                .SelectedAvatarIds;
        }

        if (GetIntParamOrDefault(3, false))
            flags |= WiredMovePhysicsFlags.BlockedByFurni;

        ctx.Policy.MovePhysics = flags;

        return Task.FromResult(true);
    }
}
