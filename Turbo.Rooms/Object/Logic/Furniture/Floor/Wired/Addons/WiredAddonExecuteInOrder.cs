using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Runs every action of the stack in stacking order, overriding a random or unseen addon
/// placed with it, and makes each variable change its own change (see
/// <see cref="Turbo.Rooms.Wired.WiredVariableChangeBatch"/>).
/// </summary>
[RoomObjectLogic("wf_xtra_exec_in_order")]
public class WiredAddonExecuteInOrder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.EXECUTE_IN_ORDER;

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.EffectMode = WiredEffectModeType.All;
        ctx.Policy.ExecuteInOrder = true;

        return Task.FromResult(true);
    }
}
