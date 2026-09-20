using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Sorts the selector pool by a variable and keeps the first N. Params: N (literal, or 1 when
/// the operand variable is used), sort mode (value or timestamps, ascending or descending),
/// operand mode, operand target. Variables: the sort key, then the operand supplying N.
/// Pool entries without the variable are dropped.
/// </summary>
public abstract class WiredAddonVariableFilter(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    protected abstract WiredVariableTargetType TargetType { get; }

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(1, 1000, 1),
            new WiredEnumParamRule<WiredVariableSortType>(WiredVariableSortType.ValueDescending),
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return Task.FromResult(true);

        var isFurni = TargetType == WiredVariableTargetType.Furni;
        var pool = isFurni
            ? ctx.SelectorPool.SelectedFurniIds
            : ctx.SelectorPool.SelectedAvatarIds.Select(x => x.Value);
        var sort = GetIntParamOrDefault(1, WiredVariableSortType.ValueDescending);
        var variableId = variable.GetVarSnapshot().VariableId;
        var keyed = new List<(int id, long key)>();

        foreach (var id in pool)
        {
            var key = new WiredVariableKey(variableId, TargetType, id);

            if (!variable.TryGetValue(key, out var value))
                continue;

            long sortKey = value;

            if (sort >= WiredVariableSortType.CreationOldestFirst)
            {
                if (!variable.TryGetTimestamps(key, out var created, out var updated))
                    continue;

                sortKey = sort >= WiredVariableSortType.UpdateOldestFirst ? updated : created;
            }

            keyed.Add((id, sortKey));
        }

        var descending =
            sort
            is WiredVariableSortType.ValueDescending
                or WiredVariableSortType.CreationNewestFirst
                or WiredVariableSortType.UpdateNewestFirst;
        var ordered = descending ? keyed.OrderByDescending(x => x.key) : keyed.OrderBy(x => x.key);
        var keep = (int)Math.Max(0, ResolveCount(ctx));
        var kept = ordered.Take(keep).Select(x => x.id).ToList();

        if (isFurni)
        {
            ctx.SelectorPool.SelectedFurniIds.Clear();
            ctx.SelectorPool.SelectedFurniIds.UnionWith(kept);
        }
        else
        {
            ctx.SelectorPool.SelectedAvatarIds.Clear();
            ctx.SelectorPool.SelectedAvatarIds.UnionWith(kept.Select(RoomObjectId.Parse));
        }

        return Task.FromResult(true);
    }

    private long ResolveCount(IWiredProcessingContext ctx)
    {
        if (!GetIntParamOrDefault(2, false))
            return GetIntParamOrDefault(0, 1);

        var operand = GetVariable(1);

        if (operand is null)
            return 1;

        var targetType = GetTargetType(operand, 3);

        foreach (var targetId in GetTargetIds(targetType, ctx.Selected))
        {
            if (ReadVariable(operand, targetType, targetId) is { } value)
                return value;
        }

        return 1;
    }
}
