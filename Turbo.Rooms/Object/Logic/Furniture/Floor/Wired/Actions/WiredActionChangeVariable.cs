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

        foreach (var targetId in GetTargetIds(targetType, selection))
        {
            var key = new WiredVariableKey(
                variable.GetVarSnapshot().VariableId,
                targetType,
                targetId
            );

            if (!variable.TryGetValue(key, out var current))
                continue;

            var next = Apply(operation, current, operand);

            if (next == current.Value)
                continue;

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

    private static int Apply(WiredVariableOperationType operation, long current, long operand)
    {
        long result = operation switch
        {
            WiredVariableOperationType.Set => operand,
            WiredVariableOperationType.Add => current + operand,
            WiredVariableOperationType.Subtract => current - operand,
            WiredVariableOperationType.Multiply => current * operand,
            WiredVariableOperationType.Divide => operand == 0 ? current : current / operand,
            WiredVariableOperationType.Modulo => operand == 0 ? current : current % operand,
            WiredVariableOperationType.Power => (long)Math.Pow(current, Math.Clamp(operand, 0, 31)),
            WiredVariableOperationType.Minimum => Math.Min(current, operand),
            WiredVariableOperationType.Maximum => Math.Max(current, operand),
            WiredVariableOperationType.Random => operand <= 0
                ? 0
                : Random.Shared.NextInt64(0, operand + 1),
            WiredVariableOperationType.Negate => -current,
            WiredVariableOperationType.BitwiseAnd => current & operand,
            WiredVariableOperationType.BitwiseOr => current | operand,
            WiredVariableOperationType.BitwiseXor => current ^ operand,
            WiredVariableOperationType.Invert => ~current,
            WiredVariableOperationType.ShiftLeft => current << (int)Math.Clamp(operand, 0, 31),
            WiredVariableOperationType.ShiftRight => current >> (int)Math.Clamp(operand, 0, 31),
            WiredVariableOperationType.Absolute => Math.Abs(current),
            WiredVariableOperationType.IsEqual => current == operand ? 1 : 0,
            WiredVariableOperationType.IsNotEqual => current != operand ? 1 : 0,
            WiredVariableOperationType.IsLess => current < operand ? 1 : 0,
            WiredVariableOperationType.IsGreater => current > operand ? 1 : 0,
            WiredVariableOperationType.SetIfLess => current < operand ? operand : current,
            WiredVariableOperationType.SetIfGreater => current > operand ? operand : current,
            WiredVariableOperationType.AddClampedToOperand => Math.Min(current + 1, operand),
            WiredVariableOperationType.SubtractClampedToOperand => Math.Max(current - 1, operand),
            WiredVariableOperationType.Average => (current + operand) / 2,
            WiredVariableOperationType.Distance => Math.Abs(current - operand),
            WiredVariableOperationType.IsLessOrEqual => current <= operand ? 1 : 0,
            WiredVariableOperationType.IsGreaterOrEqual => current >= operand ? 1 : 0,
            _ => current,
        };

        return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
    }
}
