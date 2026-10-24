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

    // A fresh box opens on 500 ms on the official server (2026-10-20T17-35-39-Open-wait).
    private const int DEFAULT_ANIMATION_MS = 500;

    private int _animationTimeMs = DEFAULT_ANIMATION_MS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        // Out of range is kept at the nearest end, off-step values as they are: 10 -> 50, 3000 ->
        // 2000, 175 stays 175 (official client capture 2026-10-24, W1 lab addon.9.E9b, 4/4).
        [new WiredClampParamRule(MIN_ANIMATION_MS, MAX_ANIMATION_MS, DEFAULT_ANIMATION_MS)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        // A second Animation Time adds to the first: 175 and 2000 on one stack move in 2175 ms
        // (official client capture 2026-10-24, W1 lab addon.9.R2, 1/1; each alone 175 / 2000).
        ctx.Policy.AnimationTimeMs = IsFirstOfKind(ctx)
            ? _animationTimeMs
            : ctx.Policy.AnimationTimeMs + _animationTimeMs;

        return Task.FromResult(true);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _animationTimeMs = GetIntParamOrDefault(0, DEFAULT_ANIMATION_MS);
    }
}
