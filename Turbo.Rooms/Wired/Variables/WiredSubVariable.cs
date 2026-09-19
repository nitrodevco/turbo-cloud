using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// A read-only variable derived from another one: the level-up and time utility addons expose
/// one per sub-value ("score.current_level", "timer.hour_of_day"). It binds to the same
/// targets as its parent and computes on read.
/// </summary>
public sealed class WiredSubVariable(
    WiredVariableId variableId,
    string name,
    IWiredVariable parent,
    Func<IWiredVariable, WiredVariableKey, WiredVariableValue?> compute
) : IWiredVariable
{
    private WiredVariableSnapshot? _snapshot;

    public bool CanBind(in WiredVariableKey key)
    {
        var snapshot = GetVarSnapshot();

        return key.VariableId == snapshot.VariableId && key.TargetType == snapshot.TargetType;
    }

    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key))
            return false;

        var parentKey = new WiredVariableKey(
            parent.GetVarSnapshot().VariableId,
            key.TargetType,
            key.TargetId
        );
        var computed = compute(parent, parentKey);

        if (computed is null)
            return false;

        value = computed.Value;

        return true;
    }

    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    ) => Task.FromResult(false);

    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    ) => Task.FromResult(false);

    public bool RemoveValue(WiredVariableKey key) => false;

    public WiredVariableSnapshot GetVarSnapshot() => _snapshot ??= Build();

    private WiredVariableSnapshot Build()
    {
        var parentSnapshot = parent.GetVarSnapshot();
        var flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;
        var connectors = new Dictionary<WiredVariableValue, string>();

        return new WiredVariableSnapshot
        {
            VariableId = variableId,
            VariableName = name,
            VariableType = WiredVariableType.Sub,
            VariableHash = WiredVariableHashBuilder.HashValues(
                name,
                parentSnapshot.AvailabilityType,
                parentSnapshot.TargetType,
                flags,
                connectors
            ),
            AvailabilityType = parentSnapshot.AvailabilityType,
            TargetType = parentSnapshot.TargetType,
            Flags = flags,
            TextConnectors = connectors,
        };
    }
}
