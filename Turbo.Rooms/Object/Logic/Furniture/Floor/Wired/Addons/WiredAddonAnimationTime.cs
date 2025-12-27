using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_anim_time")]
public class WiredAddonAnimationTime(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.ANIMATION_TIME;

    public override Task<bool> MutatePolicyAsync(IWiredContext ctx, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    public override Task BeforeEffectsAsync(IWiredContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task AfterEffectsAsync(IWiredContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
