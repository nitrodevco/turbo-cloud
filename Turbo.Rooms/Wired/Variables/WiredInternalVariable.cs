using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredInternalVariable(RoomGrain roomGrain) : IWiredInternalVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    protected abstract string VariableName { get; }

    protected virtual WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;
    protected virtual ushort Order => 10;
    protected virtual WiredVariableType VariableType => WiredVariableType.Internal;
    protected virtual WiredVariableTargetType TargetType => WiredVariableTargetType.None;
    protected virtual WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Internal;
    protected virtual WiredVariableFlags Flags => WiredVariableFlags.None;

    protected virtual Dictionary<WiredVariableValue, string> GetTextConnectors() => [];

    private WiredVariableSnapshot? _snapshot;

    public virtual bool CanBind(in WiredVariableKey key)
    {
        var snapshot = GetVarSnapshot();

        return key.VariableId == snapshot.VariableId && key.TargetType == snapshot.TargetType;
    }

    public virtual bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return false;
    }

    public virtual Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    ) => Task.FromResult(false);

    public virtual Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    ) => Task.FromResult(false);

    public virtual bool RemoveValue(WiredVariableKey key) => false;

    public WiredVariableSnapshot GetVarSnapshot() => _snapshot ??= BuildSnapshot();

    private WiredVariableSnapshot BuildSnapshot()
    {
        var variableHash = WiredVariableHashBuilder.HashValues(
            VariableName,
            AvailabilityType,
            TargetType,
            Flags,
            GetTextConnectors()
        );

        var variableId = WiredVariableIdBuilder.CreateInternalOrdered(
            TargetType,
            VariableName,
            SubBandType,
            Order
        );

        return new()
        {
            VariableId = variableId,
            VariableName = VariableName,
            VariableType = VariableType,
            VariableHash = variableHash,
            AvailabilityType = AvailabilityType,
            TargetType = TargetType,
            Flags = Flags,
            TextConnectors = GetTextConnectors(),
        };
    }
}
