using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Tunes how this stack moves things: keep altitude, move through furni, move through
/// users, blocked by furni (four checkboxes).
/// </summary>
[RoomObjectLogic("wf_xtra_mov_physics")]
public class WiredAddonMovementPhysics(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.MOVE_PHYSICS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
        ];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var flags = WiredMovePhysicsFlags.None;

        if (GetIntParamOrDefault(0, false))
            flags |= WiredMovePhysicsFlags.KeepAltitude;

        if (GetIntParamOrDefault(1, false))
            flags |= WiredMovePhysicsFlags.MoveThroughFurni;

        if (GetIntParamOrDefault(2, false))
            flags |= WiredMovePhysicsFlags.MoveThroughUsers;

        if (GetIntParamOrDefault(3, false))
            flags |= WiredMovePhysicsFlags.BlockedByFurni;

        ctx.Policy.MovePhysics = flags;

        return Task.FromResult(true);
    }
}
