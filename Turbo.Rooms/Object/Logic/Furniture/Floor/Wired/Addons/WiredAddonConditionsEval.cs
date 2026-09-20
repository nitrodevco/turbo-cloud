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
/// Changes how the conditions of the stack combine.
///
/// Int params, as the client's editor writes them: the plain mode (all, at least one, not all,
/// none), or <see cref="COUNTED"/> when one of the three counted modes was picked; then which
/// counted mode (less than, exactly, more than) and the number it compares against. The editor
/// keeps one number input per counted mode, so the two later params are zero unless a counted
/// mode is chosen.
/// </summary>
[RoomObjectLogic("wf_xtra_or_eval")]
public class WiredAddonConditionsEval(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_MODE = 0;
    private const int PARAM_COUNTED_MODE = 1;
    private const int PARAM_COUNT = 2;

    /// <summary>What the mode param holds when the player picked one of the counted modes.</summary>
    private const int COUNTED = -1;

    private const int MODE_ALL = 0;
    private const int MODE_ANY = 1;
    private const int MODE_NOT_ALL = 2;
    private const int MODE_NONE = 3;

    private const int COUNTED_LESS_THAN = 0;
    private const int COUNTED_EXACTLY = 1;
    private const int COUNTED_MORE_THAN = 2;

    // The bounds of the editor's number inputs.
    private const int COUNT_MAX = 1000;

    public override int WiredCode => (int)WiredAddonType.CONDITION_EVALUATION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(COUNTED, MODE_NONE, MODE_ALL),
            new WiredRangeParamRule(COUNTED_LESS_THAN, COUNTED_MORE_THAN, COUNTED_LESS_THAN),
            new WiredRangeParamRule(0, COUNT_MAX, 0),
        ];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var mode = GetIntParamOrDefault(PARAM_MODE, MODE_ALL);

        ctx.Policy.ConditionMode = mode switch
        {
            MODE_ANY => WiredConditionModeType.Any,
            MODE_NOT_ALL => WiredConditionModeType.NotAll,
            MODE_NONE => WiredConditionModeType.NoneMatch,
            COUNTED => GetIntParamOrDefault(PARAM_COUNTED_MODE, COUNTED_LESS_THAN) switch
            {
                COUNTED_EXACTLY => WiredConditionModeType.Exactly,
                COUNTED_MORE_THAN => WiredConditionModeType.MoreThan,
                _ => WiredConditionModeType.LessThan,
            },
            _ => WiredConditionModeType.All,
        };
        ctx.Policy.ConditionThreshold = GetIntParamOrDefault(PARAM_COUNT, 0);

        return Task.FromResult(true);
    }
}
