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
/// Runs a random pick of the actions instead of all of them. Params: how many actions at
/// the top of the stack to skip (they always run in order) and how many to pick.
/// </summary>
[RoomObjectLogic("wf_xtra_random")]
public class WiredAddonRandomActions(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.RANDOM_ACTION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 100, 0), new WiredRangeParamRule(1, 100, 1)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.EffectMode = WiredEffectModeType.Random;
        ctx.Policy.RandomSkipCount = GetIntParamOrDefault(0, 0);
        ctx.Policy.RandomPickCount = GetIntParamOrDefault(1, 1);

        return Task.FromResult(true);
    }
}
