using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;

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

    private int _animationTimeMs;

    public override Task<bool> MutatePolicyAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.AnimationTimeMs = _animationTimeMs;

        return Task.FromResult(true);
    }

    public override Task BeforeEffectsAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task AfterEffectsAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            _animationTimeMs = Math.Clamp(WiredData.IntParams?[0] ?? 50, 50, 2000);
        }
        catch { }
    }
}
