using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Applies an operation to the picked variable on the selected targets. Params: destination
/// target, operation, operand mode, operand (hi, lo), operand target. The operand comes from
/// a literal or a second variable; a few operations take none.
/// </summary>
[RoomObjectLogic("wf_act_change_var_val")]
public class WiredActionChangeVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.CHANGE_VARIABLE;

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredEnumParamRule<WiredVariableOperationType>(WiredVariableOperationType.Set),
            new WiredBoolParamRule(false),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return false;

        var selection = ctx.GetSelection(this);
        var operation = GetIntParamOrDefault(1, WiredVariableOperationType.Set);
        long operand = 0;

        if (RequiresOperand(operation) && !TryResolveOperand(2, 3, 5, 1, selection, out operand))
            return false;

        var targetType = GetTargetType(variable, 0);
        var changed = false;
        var batch = (ctx as WiredExecutionContext)?.VariableChanges;

        foreach (var targetId in GetTargetIds(targetType, selection))
        {
            var key = new WiredVariableKey(
                variable.GetVarSnapshot().VariableId,
                targetType,
                targetId
            );

            if (!variable.TryGetValue(key, out var current))
                continue;

            if (batch is not null)
            {
                // Held back: the stack's changes to this holder become one (WiredVariableChangeBatch).
                batch.Add(variable, key, operation, operand);
                changed = true;

                continue;
            }

            var next = WiredVariableOperations.Apply(operation, current, operand);

            // Written even when it stays the same: "Variable Changed" can react to "unchanged".
            changed |= await variable.SetValueAsync(ctx, key, new WiredVariableValue(next));
        }

        return changed;
    }

    private static bool RequiresOperand(WiredVariableOperationType operation) =>
        operation
            is not (
                WiredVariableOperationType.Negate
                or WiredVariableOperationType.Invert
                or WiredVariableOperationType.Absolute
            );
}
