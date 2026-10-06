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

[RoomObjectLogic("wf_xtra_anim_time")]
public class WiredAddonAnimationTime(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.ANIMATION_TIME;

    private const int MIN_ANIMATION_MS = 50;
    private const int MAX_ANIMATION_MS = 2000;

    private int _animationTimeMs = MIN_ANIMATION_MS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(MIN_ANIMATION_MS, MAX_ANIMATION_MS, MIN_ANIMATION_MS)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.AnimationTimeMs = _animationTimeMs;

        return Task.FromResult(true);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _animationTimeMs = GetIntParamOrDefault(0, MIN_ANIMATION_MS);
    }
}
