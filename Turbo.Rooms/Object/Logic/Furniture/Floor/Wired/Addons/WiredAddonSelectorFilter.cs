using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Keeps only the first N entries of the selector pool. Params: N, operand mode (literal or
/// from a variable), operand target; the variable, when used, supplies N. Applied after the
/// selectors ran and before the conditions look at the pool.
/// </summary>
public abstract class WiredAddonSelectorFilter(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    protected abstract bool FiltersFurni { get; }

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(1, 1000, 1),
            new WiredBoolParamRule(false),
            new WiredParamRule((int)WiredVariableTargetType.User),
        ];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var keep = (int)Math.Max(0, ResolveCount(ctx));

        if (FiltersFurni)
        {
            var kept = ctx.SelectorPool.SelectedFurniIds.Take(keep).ToList();

            ctx.SelectorPool.SelectedFurniIds.Clear();
            ctx.SelectorPool.SelectedFurniIds.UnionWith(kept);
        }
        else
        {
            var kept = ctx.SelectorPool.SelectedPlayerIds.Take(keep).ToList();

            ctx.SelectorPool.SelectedPlayerIds.Clear();
            ctx.SelectorPool.SelectedPlayerIds.UnionWith(kept);
        }

        return Task.FromResult(true);
    }

    private long ResolveCount(IWiredProcessingContext ctx)
    {
        long count = GetIntParamOrDefault(0, 1);

        if (!GetIntParamOrDefault(1, false))
            return count;

        var variable = GetVariable(0);

        if (variable is null)
            return count;

        var targetType = (WiredVariableTargetType)GetIntParamOrDefault(
            2,
            (int)variable.GetVarSnapshot().TargetType
        );

        foreach (var targetId in GetTargetIds(targetType, ctx.Selected))
        {
            if (ReadVariable(variable, targetType, targetId) is { } value)
                return value;
        }

        return count;
    }
}
