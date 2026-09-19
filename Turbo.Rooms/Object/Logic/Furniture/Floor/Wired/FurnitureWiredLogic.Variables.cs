using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
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

        try
        {
            return _roomGrain.WiredSystem.GetVariableById(
                WiredVariableId.Parse(_wiredData.VariableIds[index])
            );
        }
        catch (Exception ex)
        {
            LogWiredDataFault(ex);

            return null;
        }
    }

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
    /// Resolves the operand of a comparison or arithmetic box: a literal when the mode param is
    /// zero, otherwise the operand variable read on the first matching target of the selection.
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

        if (GetIntParamOrDefault(modeIndex, 0) == 0)
        {
            value = GetLongParam(hiIndex);

            return true;
        }

        var operand = GetVariable(operandVariableIndex);

        if (operand is null)
            return false;

        var targetType = (WiredVariableTargetType)GetIntParamOrDefault(
            operandTargetIndex,
            (int)operand.GetVarSnapshot().TargetType
        );

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
