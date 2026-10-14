using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>
/// Variable plumbing shared by the boxes that read or write variables: resolving a picked
/// variable, the targets a target type maps to in a selection, and the "value or another
/// variable" operand block the client writes as (mode, hi, lo, operand target).
/// </summary>
public abstract partial class FurnitureWiredLogic
{
    /// <summary>The variable picked at <paramref name="index"/>, or null when unset or gone.</summary>
    protected IWiredVariable? GetVariable(int index)
    {
        if (_wiredData is null || index < 0 || index >= _wiredData.VariableIds.Count)
            return null;

        return WiredVariableId.TryParse(_wiredData.VariableIds[index], out var variableId)
            ? WiredSystem.GetVariableById(variableId)
            : null;
    }

    /// <summary>
    /// What the box acts on: the target type saved at <paramref name="paramIndex"/>, or the
    /// variable's own when the box was never saved with one.
    /// </summary>
    protected WiredVariableTargetType GetTargetType(IWiredVariable variable, int paramIndex) =>
        GetIntParamOrDefault(paramIndex, variable.GetVarSnapshot().TargetType);

    /// <summary>The editor context of a box that lets the user pick any variable in the room.</summary>
    protected List<WiredVariableContextSnapshot> AllVariablesContext() =>
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = WiredSystem.AllVariablesHash,
            },
        ];

    /// <summary>
    /// A value the client stored as a long: two ints, the first only carrying the sign. The
    /// second int is the value itself.
    /// </summary>
    protected long GetLongParam(int hiIndex) => GetIntParamOrDefault(hiIndex + 1, 0);

    /// <summary>
    /// One merged selection of a variable box: its furni slot and its user slot of the same
    /// index together (AS3 <c>ChangeVariable.mergedSelections</c> <c>[[0,0],[1,1]]</c>: 0 the
    /// holders acted on, <c>variables_destination</c>; 1 the reference holders,
    /// <c>variables_reference</c>).
    /// </summary>
    protected IWiredSelectionSet GetMergedSelection(IWiredContext ctx, int slot) =>
        WiredSlotSelection
            .ForSlot(this, ctx, slot)
            .UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, slot));

    /// <summary>The ids a target type binds to within a selection.</summary>
    /// <summary>The ids a variable of this target type is keyed by within a selection.</summary>
    protected static IEnumerable<int> GetTargetIds(
        WiredVariableTargetType targetType,
        IWiredSelectionSet selection
    ) =>
        targetType switch
        {
            WiredVariableTargetType.Furni => selection.SelectedFurniIds,
            WiredVariableTargetType.User => selection.SelectedAvatarIds.Select(x => x.Value),
            _ => [0],
        };

    /// <summary>
    /// Resolves the operand of a comparison or arithmetic box: a literal, or, when the editor's
    /// value-or-variable switch at <paramref name="modeIndex"/> is on, the operand variable read
    /// on the first matching target of the selection. Boxes declare that switch as a
    /// <see cref="Turbo.Rooms.Wired.Rules.WiredBoolParamRule"/>.
    /// </summary>
    protected bool TryResolveOperand(
        int modeIndex,
        int hiIndex,
        int operandTargetIndex,
        int operandVariableIndex,
        IWiredSelectionSet selection,
        out long value
    )
    {
        value = 0;

        if (!GetIntParamOrDefault(modeIndex, false))
        {
            value = GetLongParam(hiIndex);

            return true;
        }

        return TryReadVariableOperand(
            operandVariableIndex,
            operandTargetIndex,
            selection,
            out value
        );
    }

    /// <summary>
    /// The variable half of a value-or-variable input: the picked variable, read on the first
    /// target of the selection that holds it. The box declares a
    /// <see cref="Turbo.Rooms.Wired.Rules.WiredRules.VariableTarget"/> rule at
    /// <paramref name="operandTargetIndex"/>.
    /// </summary>
    protected bool TryReadVariableOperand(
        int operandVariableIndex,
        int operandTargetIndex,
        IWiredSelectionSet selection,
        out long value
    )
    {
        value = 0;

        var operand = GetVariable(operandVariableIndex);

        if (operand is null)
            return false;

        var targetType = GetTargetType(operand, operandTargetIndex);

        foreach (var targetId in GetTargetIds(targetType, selection))
        {
            if (
                operand.TryGetValue(
                    new WiredVariableKey(operand.GetVarSnapshot().VariableId, targetType, targetId),
                    out var found
                )
            )
            {
                value = found;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The "same holder" half of a value-or-variable input (WIN63-202610121624
    /// <c>ValueOrVariableSection</c>, <c>wiredfurni.params.variables.reference_value.same_holder</c>):
    /// the operand variable read on the very furni or user being acted on, so "increase @altitude
    /// with @height" on a group of furni gives each its own height (Wired Faculty, 12/10/2026).
    /// False when that holder does not hold it. The editor only offers it when the reference
    /// targets the same kind as the subject, so the subject's target type is used.
    /// </summary>
    protected bool TryReadOperandOnHolder(
        int operandVariableIndex,
        WiredVariableTargetType targetType,
        int targetId,
        out long value
    )
    {
        value = 0;

        if (
            GetVariable(operandVariableIndex) is not { } operand
            || ReadVariable(operand, targetType, targetId) is not { } found
        )
            return false;

        value = found;

        return true;
    }

    /// <summary>Reads a variable on one target, null when the target has no value.</summary>
    protected static WiredVariableValue? ReadVariable(
        IWiredVariable variable,
        WiredVariableTargetType targetType,
        int targetId
    )
    {
        var key = new WiredVariableKey(variable.GetVarSnapshot().VariableId, targetType, targetId);

        return variable.TryGetValue(key, out var value) ? value : null;
    }
}
