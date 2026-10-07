using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The "Change Variable Value" boxes of one stack firing, held back and made one change. Without
/// "Execute In Order" official wired combines every change a stack makes to the same holder's
/// variable into a single change, whatever order the boxes are stacked in, applying the operations
/// in a fixed order: assignment, power, multiplication, division, modulo, addition, subtraction
/// (sirjonasxx, Wired Faculty, variables-info #14). One change means "Variable Changed" fires once.
/// </summary>
/// <remarks>
/// Operations the official order does not name (minimum, random, the bitwise ones and the rest)
/// come after subtraction, in the order their boxes ran. With "Execute In Order" there is no batch
/// and each box changes the variable as it runs.
/// </remarks>
public sealed class WiredVariableChangeBatch
{
    private readonly List<(
        IWiredVariable Variable,
        WiredVariableKey Key,
        WiredVariableOperationType Operation,
        long Operand
    )> _changes = [];

    public bool IsEmpty => _changes.Count == 0;

    public void Add(
        IWiredVariable variable,
        in WiredVariableKey key,
        WiredVariableOperationType operation,
        long operand
    ) => _changes.Add((variable, key, operation, operand));

    /// <summary>Applies every held change, one write per holder, and empties the batch.</summary>
    public async Task<bool> FlushAsync(IWiredExecutionContext ctx)
    {
        if (_changes.Count == 0)
            return false;

        var changes = _changes.ToList();
        var changed = false;

        _changes.Clear();

        foreach (var group in changes.GroupBy(x => x.Key))
        {
            var variable = group.First().Variable;

            if (!variable.TryGetValue(group.Key, out var current))
                continue;

            long value = current.Value;

            // OrderBy is stable: changes of the same rank keep the order their boxes ran in.
            foreach (var change in group.OrderBy(x => Rank(x.Operation)))
                value = WiredVariableOperations.Apply(change.Operation, value, change.Operand);

            if (value == current.Value)
                continue;

            changed |= await variable.SetValueAsync(
                ctx,
                group.Key,
                new WiredVariableValue((int)value)
            );
        }

        return changed;
    }

    private static int Rank(WiredVariableOperationType operation) =>
        operation switch
        {
            WiredVariableOperationType.Set => 0,
            WiredVariableOperationType.Power => 1,
            WiredVariableOperationType.Multiply => 2,
            WiredVariableOperationType.Divide => 3,
            WiredVariableOperationType.Modulo => 4,
            WiredVariableOperationType.Add => 5,
            WiredVariableOperationType.Subtract => 6,
            _ => 7,
        };
}
