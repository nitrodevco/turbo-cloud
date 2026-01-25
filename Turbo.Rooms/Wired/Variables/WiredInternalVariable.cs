using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredInternalVariable(RoomGrain roomGrain) : IWiredVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    protected WiredVariableSnapshot? _snapshot;

    protected abstract WiredVariableDefinition BuildVariableDefinition();

    public virtual bool CanBind(in WiredVariableBinding binding) =>
        GetVarSnapshot().TargetType == binding.TargetType;

    public virtual bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        return false;
    }

    public virtual Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    ) => Task.FromResult(false);

    public virtual bool RemoveValue(WiredVariableBinding binding) => false;

    public WiredVariableKey GetVariableKey()
    {
        var snapshot = GetVarSnapshot();

        return new WiredVariableKey(snapshot.TargetType, snapshot.VariableName);
    }

    public WiredVariableSnapshot GetVarSnapshot() =>
        _snapshot ??= BuildVariableDefinition().GetSnapshot();
}
