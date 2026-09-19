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
/// Changes how the conditions of the stack combine. Param 0 is the client evaluation mode:
/// all, any, none, not all, at least N, at most N, exactly N; params 1 and 2 carry N for
/// the counted modes.
/// </summary>
[RoomObjectLogic("wf_xtra_or_eval")]
public class WiredAddonConditionsEval(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.CONDITION_EVALUATION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 6, 0),
            new WiredRangeParamRule(0, 1000, 1),
            new WiredRangeParamRule(0, 1000, 1),
        ];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.ConditionMode = GetIntParamOrDefault(0, 0) switch
        {
            1 => WiredConditionModeType.Any,
            2 => WiredConditionModeType.NoneMatch,
            3 => WiredConditionModeType.NotAll,
            4 => WiredConditionModeType.AtLeast,
            5 => WiredConditionModeType.AtMost,
            6 => WiredConditionModeType.Exactly,
            _ => WiredConditionModeType.All,
        };
        ctx.Policy.ConditionThreshold = GetIntParamOrDefault(1, 1);

        return Task.FromResult(true);
    }
}
