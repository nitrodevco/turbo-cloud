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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Keeps N entries of the selector pool, picked at random on every firing: Furni In Area over four
/// lamps with Filter To 1 Furni toggled one lamp per click, a different one at random, repeats
/// included (official client capture 2026-10-16, 15 clicks: 135 x5, 123 x4, 133 x3, 129 x3).
/// Params: N, operand mode (literal or from a variable), operand target; the variable, when used,
/// supplies N. Applied after the selectors ran and before the conditions look at the pool.
/// </summary>
public abstract class WiredAddonSelectorFilter(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    protected abstract bool FiltersFurni { get; }

    // The holders the operand variable is read on: one furni and one user slot (official client
    // capture of the filter add-ons' sources).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.SignalUsers],
        ];

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // A fresh box keeps 10 on the official server (furni and users filters: defaults
            // [10,0,1], 2026-10-14T09-35-43-Open-wait, 2026-10-20T17-35-39-Open-wait).
            new WiredRangeParamRule(1, 1000, 10),
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var keep = (int)Math.Max(0, ResolveCount(ctx));

        if (FiltersFurni)
            KeepAtRandom(ctx.SelectorPool.SelectedFurniIds, keep);
        else
            KeepAtRandom(ctx.SelectorPool.SelectedAvatarIds, keep);

        return Task.FromResult(true);
    }

    private static void KeepAtRandom<T>(ISet<T> pool, int keep)
    {
        if (pool.Count <= keep)
            return;

        var all = pool.ToArray();

        Random.Shared.Shuffle(all);

        pool.Clear();
        pool.UnionWith(all.Take(keep));
    }

    private long ResolveCount(IWiredProcessingContext ctx)
    {
        long count = GetIntParamOrDefault(0, 10);

        if (!GetIntParamOrDefault(1, false))
            return count;

        var variable = GetVariable(0);

        if (variable is null)
            return count;

        var targetType = GetTargetType(variable, 2);

        foreach (var targetId in GetTargetIds(targetType, GetMergedSelection(ctx, 0)))
        {
            if (ReadVariable(variable, targetType, targetId) is { } value)
                return value;
        }

        return count;
    }
}
