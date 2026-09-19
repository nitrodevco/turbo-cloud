using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

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
            ? _roomGrain.WiredSystem.GetVariableById(variableId)
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
                AllVariablesHash = _roomGrain.WiredSystem.AllVariablesHash,
            },
        ];

    /// <summary>
    /// A value the client stored as a long: two ints, the first only carrying the sign. The
    /// second int is the value itself.
    /// </summary>
    protected long GetLongParam(int hiIndex) => GetIntParamOrDefault(hiIndex + 1, 0);

    /// <summary>The ids a target type binds to within a selection.</summary>
    protected static IEnumerable<int> GetTargetIds(
        WiredVariableTargetType targetType,
        IWiredSelectionSet selection
    ) =>
        targetType switch
        {
            WiredVariableTargetType.Furni => selection.SelectedFurniIds,
            WiredVariableTargetType.User => selection.SelectedPlayerIds,
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
